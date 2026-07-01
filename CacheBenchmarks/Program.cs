using SocialApi.Caching;

Console.WriteLine("==================================================");
Console.WriteLine("Running Skewed Access Benchmark (80/20 Zipfian Rule)");
Console.WriteLine("==================================================");

int numRequests = 200_000;
int numUniqueKeys = 1000;
int cacheCapacity = 150; // Constrained memory to trigger high eviction rates

var trafficSimulation = new List<int>(numRequests);
var rand = new Random(42); // Seeded for absolute deterministic comparison

// Step 1: Generate Skewed Traffic Pattern
// 80% of operations target the top 20% "Hot Keys"
int hotKeyThreshold = (int)(numUniqueKeys * 0.20); // Keys 1 to 200

for (int i = 0; i < numRequests; i++)
{
    if (rand.NextDouble() < 0.80)
    {
        trafficSimulation.Add(rand.Next(1, hotKeyThreshold + 1));
    }
    else
    {
        trafficSimulation.Add(rand.Next(hotKeyThreshold + 1, numUniqueKeys + 1));
    }
}

// Step 2: Test LRU Cache
var lruCache = new LRUCache<int, string>(cacheCapacity);
int lruHits = 0;

foreach (var key in trafficSimulation)
{
    if (lruCache.Get(key) != null)
    {
        lruHits++;
    }
    else
    {
        lruCache.Put(key, "PayloadData");
    }
}

// Step 3: Test LFU Cache
var lfuCache = new LFUCache<int, string>(cacheCapacity);
int lfuHits = 0;

foreach (var key in trafficSimulation)
{
    if (lfuCache.Get(key) != null)
    {
        lfuHits++;
    }
    else
    {
        lfuCache.Put(key, "PayloadData");
    }
}

// Step 4: Display Comparative Results
double lruHitRatio = ((double)lruHits / numRequests) * 100;
double lfuHitRatio = ((double)lfuHits / numRequests) * 100;

Console.WriteLine($"Total Simulated Requests: {numRequests:N0}");
Console.WriteLine($"Cache Capacity Constraint: {cacheCapacity} keys\n");
Console.WriteLine($"-> LRU Total Hits: {lruHits:N0} | Hit Ratio: {lruHitRatio:F2}%");
Console.WriteLine($"-> LFU Total Hits: {lfuHits:N0} | Hit Ratio: {lfuHitRatio:F2}%");
Console.WriteLine("==================================================");

if (lfuHitRatio > lruHitRatio)
{
    Console.WriteLine("Result: LFU outperformed LRU. Frequency tracking prevents frequently accessed data from being prematurely evicted by transient noise.");
}