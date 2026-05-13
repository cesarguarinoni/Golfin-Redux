using NUnit.Framework;
using Golfin.Physics;
using Golfin.Physics.Viewer;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// §2f: Unit tests for PutterModeSurfaceController.DecideTargetClub.
    /// Pure logic — no scene setup, no GameSession state, no [SetUp] needed.
    /// </summary>
    public class PutterModeSurfaceControllerTests
    {
        const int PutterIndex = 3;

        // 1. On green, not in putter mode → returns putterIndex
        [Test]
        public void DecideTargetClub_OnGreenAndNotPutter_ReturnsPutterIndex()
        {
            int result = PutterModeSurfaceController.DecideTargetClub(
                currentClubIndex: 0,
                putterIndex: PutterIndex,
                endSurface: SurfaceType.Green,
                lastNonPutterClubIndex: 0);
            Assert.AreEqual(PutterIndex, result);
        }

        // 2. On green, already in putter mode → no change (-1)
        [Test]
        public void DecideTargetClub_OnGreenAndAlreadyPutter_ReturnsNoChange()
        {
            int result = PutterModeSurfaceController.DecideTargetClub(
                currentClubIndex: PutterIndex,
                putterIndex: PutterIndex,
                endSurface: SurfaceType.Green,
                lastNonPutterClubIndex: 0);
            Assert.AreEqual(-1, result);
        }

        // 3. Off green (Fairway), in putter mode → returns lastNonPutterClubIndex (Iron 7 = 1)
        [Test]
        public void DecideTargetClub_OffGreenAndInPutter_ReturnsLastNonPutter()
        {
            int result = PutterModeSurfaceController.DecideTargetClub(
                currentClubIndex: PutterIndex,
                putterIndex: PutterIndex,
                endSurface: SurfaceType.Fairway,
                lastNonPutterClubIndex: 1);
            Assert.AreEqual(1, result);
        }

        // 4. Off green (Rough), not in putter mode → no change (-1)
        [Test]
        public void DecideTargetClub_OffGreenAndNotPutter_ReturnsNoChange()
        {
            int result = PutterModeSurfaceController.DecideTargetClub(
                currentClubIndex: 0,
                putterIndex: PutterIndex,
                endSurface: SurfaceType.Rough,
                lastNonPutterClubIndex: 0);
            Assert.AreEqual(-1, result);
        }

        // 5. GreenCollar is NOT Green per L1 strict rule — not currently putter → no change
        [Test]
        public void DecideTargetClub_GreenCollarIsNotGreen_KeepsNonPutter()
        {
            int result = PutterModeSurfaceController.DecideTargetClub(
                currentClubIndex: 1,
                putterIndex: PutterIndex,
                endSurface: SurfaceType.GreenCollar,
                lastNonPutterClubIndex: 1);
            Assert.AreEqual(-1, result);
        }

        // 6. All non-Green surfaces trigger exit from putter when in putter mode.
        //    Water/OOB excluded — those terminate as OB, not AtRest.
        [TestCase(SurfaceType.Fairway)]
        [TestCase(SurfaceType.GreenCollar)]
        [TestCase(SurfaceType.Semirough)]
        [TestCase(SurfaceType.Rough)]
        [TestCase(SurfaceType.Tee)]
        [TestCase(SurfaceType.Sand)]
        [TestCase(SurfaceType.BunkerLip)]
        [TestCase(SurfaceType.CartPath)]
        public void DecideTargetClub_AllNonGreenSurfacesTriggerExitFromPutter(SurfaceType surface)
        {
            int lastNonPutter = 2; // Wedge
            int result = PutterModeSurfaceController.DecideTargetClub(
                currentClubIndex: PutterIndex,
                putterIndex: PutterIndex,
                endSurface: surface,
                lastNonPutterClubIndex: lastNonPutter);
            Assert.AreEqual(lastNonPutter, result,
                $"Surface {surface} while in putter should return lastNonPutter={lastNonPutter}");
        }
    }
}
