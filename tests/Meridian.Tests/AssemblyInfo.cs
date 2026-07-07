using Xunit;

// Several test classes call Services.Reset() / register fakes against the process-global, non-thread-safe
// static Services locator, while domain classes under test in other classes read from it concurrently.
// xUnit parallelizes test collections by default, which makes that a latent race. The suite runs in tens
// of milliseconds, so serializing it removes the hazard with no meaningful cost (T3).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
