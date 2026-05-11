// §2d — allow Golfin.Physics.Tests to access internal test seams
// in Golfin.Gameplay.UI (HoleCompleteCardWidget's header/button accessors).
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Golfin.Physics.Tests")]
