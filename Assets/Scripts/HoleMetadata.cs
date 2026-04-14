using UnityEngine;

namespace Golfin.CourseImport
{
    public class HoleMetadata : MonoBehaviour
    {
        public string courseId;
        public int holeNumber;
        public int par;
        public int strokeIndex;
        public int championshipYards;
        public string reviewStatus;
        /// <summary>Import pipeline that created this scene: Lite | LiteFlat | Geo | GeoFlat</summary>
        public string importType;
    }
}
