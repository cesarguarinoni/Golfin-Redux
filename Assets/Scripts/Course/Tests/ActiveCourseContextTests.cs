using NUnit.Framework;
using Golfin.Gameplay.Loop;   // ActiveCourseContext

namespace Golfin.Course.Tests
{
    /// <summary>
    /// EditMode unit tests for ActiveCourseContext (SPEC §3, Phase 3).
    /// Verifies Set/Reset round-trip and slug invariants.
    /// </summary>
    public class ActiveCourseContextTests
    {
        [SetUp]
        public void SetUp()
        {
            // Ensure we start from a clean default before each test.
            ActiveCourseContext.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            // Restore default after each test so other suites are not polluted.
            ActiveCourseContext.Reset();
        }

        [Test]
        public void DefaultSlug_IsLomondCountryClub()
        {
            // The default course is "lomond-country-club" (SPEC §3).
            Assert.AreEqual("lomond-country-club", ActiveCourseContext.CurrentCourseSlug);
        }

        [Test]
        public void Set_UpdatesCurrentCourseSlug()
        {
            ActiveCourseContext.Set("sunrise-links", "Sunrise Links");
            Assert.AreEqual("sunrise-links", ActiveCourseContext.CurrentCourseSlug);
        }

        [Test]
        public void Set_UpdatesCurrentCourseDisplayName()
        {
            ActiveCourseContext.Set("sunrise-links", "Sunrise Links");
            Assert.AreEqual("Sunrise Links", ActiveCourseContext.CurrentCourseDisplayName);
        }

        [Test]
        public void Reset_RestoresDefaultSlug()
        {
            ActiveCourseContext.Set("sunrise-links", "Sunrise Links");
            ActiveCourseContext.Reset();
            Assert.AreEqual("lomond-country-club", ActiveCourseContext.CurrentCourseSlug);
        }

        [Test]
        public void OnCourseChanged_FiresOnSet()
        {
            bool fired = false;
            ActiveCourseContext.OnCourseChanged += () => fired = true;
            try
            {
                ActiveCourseContext.Set("test-course", "Test Course");
                Assert.IsTrue(fired, "OnCourseChanged should fire when Set() is called.");
            }
            finally
            {
                // Unsubscribe to avoid leaking state between tests.
                ActiveCourseContext.OnCourseChanged -= () => fired = true;
            }
        }
    }
}
