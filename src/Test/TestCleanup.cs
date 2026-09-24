using NUnit.Framework;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace WinMemoryCleaner.Test
{
    /// <summary>
    /// Cleanup that resets settings to defaults after all other tests complete.
    /// </summary>
    [SetUpFixture]
    public sealed class TestCleanup
    {
        // NUnit 3 requires the namespace-level teardown hook to be OneTimeTearDown;
        // a plain [TearDown] inside a SetUpFixture is rejected.
        [OneTimeTearDown]
        public void ResetSettingsAfterAllTests()
        {
            Settings.Reset(true);
        }
    }
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
