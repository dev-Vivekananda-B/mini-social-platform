# Mini Social Platform

## Setup Steps
1. Ensure [.NET 8 SDK](https://dotnet.microsoft.com/download) is installed.
2. Clone this repository: `git clone https://github.com/dev-Vivekananda-B/mini-social-platform.git`
3. Run the API: `dotnet run --project SocialApi`
4. Run Tests: `dotnet test`
5. Run Benchmarks: `dotnet run --project CacheBenchmarks`

## Feed Endpoint Latency Metrics (Cache-Aside)
* **Cold Cache (Before):** ~505ms (Simulated DB Latency)
* **Warm Cache (After):** < 1ms (Served from LRU Memory)

**Server Side**
<img width="887" height="604" alt="image" src="https://github.com/user-attachments/assets/5b9c34ff-f0ae-4209-8748-dfb5f7b71799" />

** BenchMarks and Client side API calls **
<img width="1866" height="876" alt="image" src="https://github.com/user-attachments/assets/2ec5cac9-71fe-478c-9294-7c4a2f174716" />

1) Test the Cold Cache (Simulating a DB Hit)
2) Test the Warm Cache (Simulating Memory Hit)
3) Test Cache Invalidation (Fan-out on Write)
4) Verify the Invalidation Worked
