\# Design Document: Adding Caching to Instagram Feed



\## 1. Overview

The Instagram feed is a massively read-heavy system (typically a 100:1 read-to-write ratio). Fetching a personalized feed directly from a relational database for every request would result in catastrophic latency and database bottlenecking. 



To achieve sub-100ms latencies and handle millions of concurrent users, we must implement a multi-tiered caching strategy. This document outlines what data to cache, where it should live, how it is invalidated, and how we protect the backend from sudden traffic spikes.



\---



\## 2. What to Cache

To optimize the feed loading process, we must decouple the \*structure\* of the feed from the \*content\* of the posts.



\* \*\*User Feed Lists:\*\* A list of `Post\_IDs` tailored to a specific user (e.g., `user\_123\_feed: \[post\_89, post\_90, post\_102]`). 

\* \*\*Post Metadata:\*\* The actual text and metadata of a post (Author ID, Caption, Timestamp, Like Count, Media URLs). Cached as a simple Key-Value pair (e.g., `post\_89: { ... }`).

\* \*\*Media Assets:\*\* The heavy payload—images and videos.

\* \*\*User Profiles:\*\* Basic author information required to render the UI (Avatar URL, Username).



\---



\## 3. Where to Cache (The Caching Tiers)

We will utilize a three-tier caching architecture to serve data as close to the user as possible.



\### A. Client-Side (Mobile App / Browser)

\* \*\*What:\*\* Local media assets, the most recent snapshot of the user's feed, and user profile data.

\* \*\*How:\*\* \* Mobile apps use local embedded databases (SQLite/CoreData) to store the last loaded feed, allowing the app to render instantly on launch (offline mode) while fetching updates in the background.

&#x20; \* HTTP `Cache-Control` headers instruct the client to cache static images locally.



\### B. CDN (Content Delivery Network)

\* \*\*What:\*\* Media Assets (Images, Videos) and static frontend bundles.

\* \*\*How:\*\* Media URLs point to a globally distributed CDN (e.g., Cloudflare, AWS CloudFront). When a user in Tokyo requests a video hosted in a US database, the CDN serves a cached copy from a Tokyo edge server, drastically reducing latency and network costs.



\### C. Server-Side (Distributed In-Memory Cache)

\* \*\*What:\*\* Feed Lists and Post Metadata.

\* \*\*How:\*\* We use a distributed, memory-resident datastore like \*\*Redis\*\* or \*\*Memcached\*\*. 

&#x20; \* \*\*Redis\*\* is preferred for Feed Lists because its native `List` and `Sorted Set` data structures make it trivial to append new `Post\_IDs` or paginate through a feed chronologically.



\---



\## 4. Invalidation Triggers \& Strategies

Stale data is the enemy of a social platform. We utilize a combination of Push and Pull models to ensure cache coherency.



\### A. Post Creation (Fan-out on Write vs. Fan-out on Load)

When a user publishes a post, we must update their followers' feeds. 

\* \*\*Standard Users (Push / Fan-out on Write):\*\* If a user has 500 followers, we synchronously append the new `Post\_ID` to the cached Feed Lists of all 500 followers.

\* \*\*Celebrity Users (Pull / Fan-out on Load):\*\* If Cristiano Ronaldo (600M+ followers) posts, a Push model would crash the cache nodes. Instead, we do \*not\* push to followers. When a follower loads their feed, the system checks a "celebrity post table," merges recent celebrity posts with the user's cached feed in memory, and returns the result.



\### B. Post Deletion or Updates

\* We use a \*\*Cache-Aside\*\* pattern. When a post is deleted or the caption is edited, the application updates the database and immediately issues an `EXPIRE` or `DEL` command to the specific `post\_{id}` key in Redis. 



\### C. Follow / Unfollow Actions

\* Following a new user triggers an asynchronous background job to fetch the new followee's recent posts and merge them into the current user's cached Feed List.



\---



\## 5. Eviction Policy

Because memory is expensive, the cache cannot grow infinitely. 



\* \*\*Post Metadata \& Media:\*\* \*\*LFU (Least Frequently Used)\*\*. In a social network, traffic follows a Zipfian (Power-Law) distribution. 20% of the posts (viral content/celebrities) generate 80% of the views. LFU ensures highly accessed viral posts stay in memory, even if temporarily superseded by a burst of newer, less popular posts.

\* \*\*User Feed Lists:\*\* \*\*LRU (Least Recently Used) + TTL\*\*. We only cache the feeds of \*active\* users. We set a Time-To-Live (TTL) of 7 days on feed lists. If a user doesn't log in for a week, their feed is evicted to save space. When they return, it is recomputed.



\---



\## 6. Stampede Protection (Thundering Herd)

A "Cache Stampede" occurs when a highly popular cached item (like a viral celebrity post) expires or is invalidated. Suddenly, 100,000 concurrent requests miss the cache and hit the database simultaneously, causing it to buckle.



We employ three strategies to mitigate this:



1\. \*\*Request Coalescing (Promise Collapsing):\*\* If the cache is cold, the application layer locks the key. Only the \*first\* thread queries the database. The other 99,999 threads wait for the first thread to populate the cache, then read from memory.

2\. \*\*TTL Jitter:\*\* Instead of setting a strict 1-hour TTL on a batch of posts, we add random "jitter" (e.g., TTL = 60 minutes ± 5 minutes). This prevents massive blocks of keys from expiring at the exact same millisecond.

3\. \*\*Probabilistic Early Expiration (XFetch):\*\* For highly requested keys, the application artificially treats the cache as "expired" a few seconds before the actual TTL hits for a random subset of requests. One background thread recomputes the data and updates the cache \*before\* it actually drops, ensuring 100% cache uptime for the remaining traffic.

