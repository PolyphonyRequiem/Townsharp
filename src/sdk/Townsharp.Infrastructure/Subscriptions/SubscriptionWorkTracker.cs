﻿using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace Townsharp.Infrastructure.Subscriptions;

internal class SubscriptionWorkTracker
{
    private readonly ConcurrentDictionary<SubscriptionDefinition, SubscriptionIntent> trackedSubscriptions = new();
    private readonly ConcurrentDictionary<SubscriptionDefinition, SubscriptionDisposition> subscriptionDispositions = new();

    private readonly ILogger logger;

    internal SubscriptionWorkTracker(ILogger logger)
    {
        this.logger = logger;
    }

    internal void AddSubscriptions(SubscriptionDefinition[] subscriptionDefinitions)
    {
        this.ReconcileTrackedSubscriptions(subscriptionDefinitions, SubscriptionIntent.Subscribed);
    }

    internal void AddUnsubscriptions(SubscriptionDefinition[] subscriptionDefinitions)
    {
        this.ReconcileTrackedSubscriptions(subscriptionDefinitions, SubscriptionIntent.Unsubscribed);
    }

    internal SubscriptionWorkLease[] TakeWorkLeases(int maxLeases)
    {
        var leaseCandidates = new List<SubscriptionWorkLease>();

        // first pass, let's find work and claim it.

        foreach (var item in this.subscriptionDispositions)
        {
            if (item.Value == SubscriptionDisposition.New || item.Value == SubscriptionDisposition.RetryNeeded)
            {
                if (this.subscriptionDispositions.TryUpdate(item.Key, SubscriptionDisposition.Working, item.Value))
                {
                    if (!this.trackedSubscriptions.TryGetValue(item.Key, out var intent))
                    {
                        continue;
                    }

                    leaseCandidates.Add(new SubscriptionWorkLease(item.Key, intent, item.Value));
                }
            }
        }

        // second pass, priority reconciliation and limiting.

        var leasesToReturn = new List<SubscriptionWorkLease>();

        var predicatePriorities = new Func<SubscriptionWorkLease, bool>[]
        {
            lease => lease.Intent == SubscriptionIntent.Subscribed && lease.PriorDisposition == SubscriptionDisposition.New,
            lease => lease.Intent == SubscriptionIntent.Subscribed && lease.PriorDisposition == SubscriptionDisposition.RetryNeeded,
            lease => lease.Intent == SubscriptionIntent.Unsubscribed && lease.PriorDisposition == SubscriptionDisposition.New,
            lease => lease.Intent == SubscriptionIntent.Unsubscribed && lease.PriorDisposition == SubscriptionDisposition.RetryNeeded
        };

        foreach (var predicate in predicatePriorities)
        {
            foreach (var workLease in leaseCandidates)
            {
                if (predicate(workLease))
                {
                    leasesToReturn.Add(workLease);
                }

                if (leasesToReturn.Count >= maxLeases)
                {
                    break;
                }
            }

            if (leasesToReturn.Count >= maxLeases)
            {
                break;
            }
        }

        // restore the prior states for the unclaimed leases.
        foreach (var lease in leaseCandidates.Except(leasesToReturn))
        {
            this.subscriptionDispositions.TryUpdate(lease.SubscriptionDefinition, lease.PriorDisposition, SubscriptionDisposition.Working);
        }

        return leasesToReturn.ToArray();
    }

    internal void ReportLeaseResolved(SubscriptionWorkLease lease)
    {
        if (lease.Intent == SubscriptionIntent.Unsubscribed)
        {
            if (this.subscriptionDispositions.TryGetValue(lease.SubscriptionDefinition, out var disposition) && disposition == SubscriptionDisposition.Cancelled)
            {
                this.SetDispositionForNewSubscribedIntent(lease.SubscriptionDefinition);
            }

            this.subscriptionDispositions.TryRemove(lease.SubscriptionDefinition, out _);
        }
        else // subscribed
        {
            this.subscriptionDispositions.TryUpdate(lease.SubscriptionDefinition, SubscriptionDisposition.Resolved, SubscriptionDisposition.Working);
        }
    }

    internal void ReportLeaseRetryNeeded(SubscriptionWorkLease lease)
    {
        if (lease.Intent == SubscriptionIntent.Unsubscribed)
        {
            if (this.subscriptionDispositions.TryGetValue(lease.SubscriptionDefinition, out var disposition) && disposition == SubscriptionDisposition.Cancelled)
            {
                this.SetDispositionForNewSubscribedIntent(lease.SubscriptionDefinition);
            }

            this.subscriptionDispositions.TryRemove(lease.SubscriptionDefinition, out _);
        }
        else // subscribed
        {
            this.subscriptionDispositions.TryUpdate(lease.SubscriptionDefinition, SubscriptionDisposition.RetryNeeded, SubscriptionDisposition.Working);
        }
    }

    internal void ReportLeaseInvalidSubscription(SubscriptionWorkLease lease)
    {
        if (lease.Intent == SubscriptionIntent.Unsubscribed)
        {
            if (this.subscriptionDispositions.TryGetValue(lease.SubscriptionDefinition, out var disposition) && disposition == SubscriptionDisposition.Cancelled)
            {
                this.SetDispositionForNewSubscribedIntent(lease.SubscriptionDefinition);
            }

            this.subscriptionDispositions.TryRemove(lease.SubscriptionDefinition, out _);
        }
        else // subscribed
        {
            this.subscriptionDispositions.TryUpdate(lease.SubscriptionDefinition, SubscriptionDisposition.InvalidSubscription, SubscriptionDisposition.Working);
        }
    }

    internal void ResetDispositionsForRecovery()
    {
        // We likely don't need to worry -as- about concurrency here as nothing should be processing work while we are in a recovery state.
        // We should reconcile all intents and dispositions.
        List<SubscriptionDefinition> unsubscriptionIntentsToRemove = new();

        foreach (var intent in this.trackedSubscriptions)
        {
            if (intent.Value == SubscriptionIntent.Subscribed)
            {
                this.subscriptionDispositions[intent.Key] = SubscriptionDisposition.New;
            }
            else // Unsubcribed
            {
                unsubscriptionIntentsToRemove.Add(intent.Key);
                this.subscriptionDispositions.TryRemove(intent.Key, out _);
            }
        }

        foreach (var intentToRemove in unsubscriptionIntentsToRemove)
        {
            this.trackedSubscriptions.TryRemove(intentToRemove, out _);
        }
    }

    private void ReconcileTrackedSubscriptions(SubscriptionDefinition[] subscriptionDefinitions, SubscriptionIntent intent)
    {
        foreach (var subscriptionDefinition in subscriptionDefinitions)
        {
            if (intent == SubscriptionIntent.Subscribed)
            {
                this.TrackForSubscription(subscriptionDefinition);
            }
            else
            {
                this.TrackForUnsubscription(subscriptionDefinition);
            }
        }
    }

    private void TrackForSubscription(SubscriptionDefinition subscriptionDefinition)
    {
        do
        {
            if (this.trackedSubscriptions.TryAdd(subscriptionDefinition, SubscriptionIntent.Subscribed))
            {
                this.SetDispositionForNewSubscribedIntent(subscriptionDefinition);
                return;
            }
            else
            {
                // we already have an intent, so we need to change it.
                if (this.trackedSubscriptions.TryUpdate(subscriptionDefinition, SubscriptionIntent.Subscribed, SubscriptionIntent.Unsubscribed))
                {
                    if (this.ChangeIntentToSubscribed(subscriptionDefinition))
                    {
                        // success! all done.
                        return;
                    }

                    continue;
                }
                else
                {
                    // we failed to update, so either it has been unsubscribed, or it is now subscribed.
                    // let's just try again.
                    continue;
                }
            }
        }
        while (true);
    }

    private void SetDispositionForNewSubscribedIntent(SubscriptionDefinition subscriptionDefinition)
    {
        if (!this.subscriptionDispositions.TryAdd(subscriptionDefinition, SubscriptionDisposition.New))
        {
            // something went wrong, this already exists.
            if (this.subscriptionDispositions.TryGetValue(subscriptionDefinition, out var disposition))
            {
                if (disposition == SubscriptionDisposition.InvalidSubscription)
                {
                    // let's give it another go?
                    this.subscriptionDispositions.TryUpdate(subscriptionDefinition, SubscriptionDisposition.New, disposition);
                }
            }
        }
    }

    private bool ChangeIntentToSubscribed(SubscriptionDefinition subscriptionDefinition)
    {
        // we intended to be unsubscribed, so we need to update the disposition.
        if (!this.trackedSubscriptions.TryUpdate(subscriptionDefinition, SubscriptionIntent.Subscribed, SubscriptionIntent.Unsubscribed))
        {
            // either this is no longer intended for unsubscription, or it is no longer in the collection (meaning it was unsubscribed and removed)
            // let's just try again.

            var currentDisposition = this.subscriptionDispositions.GetOrAdd(subscriptionDefinition, SubscriptionDisposition.New);

            if (currentDisposition == SubscriptionDisposition.New)
            {
                return true;
            }

            if (currentDisposition == SubscriptionDisposition.Working)
            {
                // too late, let's just sort it out when the work finishes.
                // set the value to Cancelled
                this.subscriptionDispositions.TryUpdate(subscriptionDefinition, SubscriptionDisposition.Cancelled, currentDisposition);
                // pass or fail, we are done.
                return true;
            }

            return this.subscriptionDispositions.TryUpdate(subscriptionDefinition, SubscriptionDisposition.New, currentDisposition);
        }

        return true;
    }

    private void TrackForUnsubscription(SubscriptionDefinition subscriptionDefinition)
    {
        do
        {
            if (this.trackedSubscriptions.TryAdd(subscriptionDefinition, SubscriptionIntent.Unsubscribed))
            {
                this.SetDispositionForNewUnsubscribedIntent(subscriptionDefinition);
                return;
            }
            else
            {
                // we already have an intent, so we need to change it.
                if (this.trackedSubscriptions.TryUpdate(subscriptionDefinition, SubscriptionIntent.Unsubscribed, SubscriptionIntent.Subscribed))
                {
                    this.ChangeIntentToUnsubscribed(subscriptionDefinition);
                    // success! all done.
                    return;
                }
                else
                {
                    // we failed to update, so either it has been subscribed, or it is now unsubscribed.
                    // let's just try again.
                    continue;
                }
            }
        }
        while (true);
    }

    private void SetDispositionForNewUnsubscribedIntent(SubscriptionDefinition subscriptionDefinition)
    {
        // don't care, it's new, we will sort it out on completion if it was in flight.
        this.subscriptionDispositions.AddOrUpdate(subscriptionDefinition, SubscriptionDisposition.New, (_, _) => SubscriptionDisposition.New);
    }

    private void ChangeIntentToUnsubscribed(SubscriptionDefinition subscriptionDefinition)
    {
        var currentDisposition = this.subscriptionDispositions.GetOrAdd(subscriptionDefinition, SubscriptionDisposition.New);

        if (currentDisposition == SubscriptionDisposition.Resolved)
        {
            // well, looks like we have to unsubscribe...
            this.subscriptionDispositions.AddOrUpdate(subscriptionDefinition, SubscriptionDisposition.New, (_, _) => SubscriptionDisposition.New);
        }
        else if (currentDisposition == SubscriptionDisposition.Working)
        {
            // too late, let's just sort it out when the work finishes.
            // set the value to Cancelled and we will handle it later.
            this.subscriptionDispositions.TryUpdate(subscriptionDefinition, SubscriptionDisposition.Cancelled, currentDisposition);
        }
        else
        {
            // guess we are done! let's just remove it!
            this.trackedSubscriptions.TryRemove(subscriptionDefinition, out _);
        }
    }
}

internal enum SubscriptionDisposition
{
    New,
    Working,
    Cancelled,
    Resolved,
    RetryNeeded,
    InvalidSubscription
}

internal enum SubscriptionIntent
{
    Subscribed,
    Unsubscribed
}

internal record SubscriptionWorkLease(SubscriptionDefinition SubscriptionDefinition, SubscriptionIntent Intent, SubscriptionDisposition PriorDisposition);