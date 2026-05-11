// §2d — allow Golfin.Physics.Tests to access internal test seams
// in Golfin.Physics.Viewer (HoleCompleteDriver.InjectForTests, HandleShotCompleteForTest).
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Golfin.Physics.Tests")]
