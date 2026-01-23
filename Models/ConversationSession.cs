using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Aegis.Api.Models
{
    /// <summary>
    /// A thread-safe in-memory conversation session.
    /// Stores message history and aggregated extracted intelligence.
    /// </summary>
    public sealed class ConversationSession
    {
        private readonly object _sync = new();

        public string SessionId { get; }
        public DateTimeOffset CreatedAt { get; }

        /// <summary>
        /// Updated on every access or mutation.
        /// </summary>
    /// <summary>
    /// Updated on every access or mutation (UTC).
    /// </summary>
    public DateTime LastUpdatedUtc { get; private set; }

        /// <summary>
        /// Thread-safe append-only message history.
        /// Use Concurrency-friendly structure so readers can enumerate safely.
        /// </summary>
        public ConcurrentQueue<MessageEntry> History { get; } = new();

        /// <summary>
        /// Aggregated intelligence collected across messages in this session.
        /// Access/modification is protected by an internal lock to ensure consistency.
        /// </summary>
        public ExtractedIntelligence AggregatedIntelligence { get; private set; } = new ExtractedIntelligence();

        public ConversationSession(string sessionId)
        {
            SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
            CreatedAt = DateTimeOffset.UtcNow;
            LastUpdatedUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Append a message to the session history and update last accessed timestamp.
        /// </summary>
        public void AppendMessage(string role, string content, DateTimeOffset? timestamp = null)
        {
            var entry = new MessageEntry
            {
                Role = role ?? "user",
                Content = content ?? string.Empty,
                Timestamp = timestamp ?? DateTimeOffset.UtcNow
            };

            History.Enqueue(entry);
            Touch();
        }

        /// <summary>
        /// Merge extracted intelligence into the aggregated store.
        /// This method is thread-safe.
        /// </summary>
    public void MergeExtractedIntelligence(ExtractedIntelligence? intel)
        {
            if (intel is null) return;

            lock (_sync)
            {
                // merge UPI ids
                if (intel.UpiIds != null)
                {
                    AggregatedIntelligence.UpiIds ??= new List<string>();
                    foreach (var id in intel.UpiIds)
                    {
                        if (!AggregatedIntelligence.UpiIds.Contains(id)) AggregatedIntelligence.UpiIds.Add(id);
                    }
                }

                // merge phone numbers
                if (intel.PhoneNumbers != null)
                {
                    AggregatedIntelligence.PhoneNumbers ??= new List<string>();
                    foreach (var n in intel.PhoneNumbers)
                    {
                        if (!AggregatedIntelligence.PhoneNumbers.Contains(n)) AggregatedIntelligence.PhoneNumbers.Add(n);
                    }
                }

                // merge urls
                if (intel.Urls != null)
                {
                    AggregatedIntelligence.Urls ??= new List<string>();
                    foreach (var u in intel.Urls)
                    {
                        if (!AggregatedIntelligence.Urls.Contains(u)) AggregatedIntelligence.Urls.Add(u);
                    }
                }

                // merge bank accounts by account number
                if (intel.BankAccounts != null)
                {
                    AggregatedIntelligence.BankAccounts ??= new List<BankAccount>();
                    var existing = new HashSet<string>(AggregatedIntelligence.BankAccounts.Select(b => b.AccountNumber));
                    foreach (var b in intel.BankAccounts)
                    {
                        if (b == null || string.IsNullOrWhiteSpace(b.AccountNumber)) continue;
                        if (!existing.Contains(b.AccountNumber))
                        {
                            AggregatedIntelligence.BankAccounts.Add(new BankAccount { AccountNumber = b.AccountNumber, Ifsc = b.Ifsc });
                            existing.Add(b.AccountNumber);
                        }
                    }
                }

                Touch();
            }
        }

        /// <summary>
        /// Update the LastAccessed timestamp to now.
        /// </summary>
        public void Touch()
        {
            lock (_sync)
            {
                LastUpdatedUtc = DateTime.UtcNow;
            }
        }
    }

    public sealed class MessageEntry
    {
        public string Role { get; init; } = "user";
        public string Content { get; init; } = string.Empty;
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    }
}
