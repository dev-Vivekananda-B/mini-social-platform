# Mini Social Platform

## Setup Steps
1. Ensure [.NET 8 SDK](https://dotnet.microsoft.com/download) is installed.
2. Clone this repository: `git clone <repo-url>`
3. Run the API: `dotnet run --project SocialApi`
4. Run Tests: `dotnet test`
5. Run Benchmarks: `dotnet run --project CacheBenchmarks`

## Feed Endpoint Latency Metrics (Cache-Aside)
* **Cold Cache (Before):** ~505ms (Simulated DB Latency)
* **Warm Cache (After):** < 1ms (Served from LRU Memory)