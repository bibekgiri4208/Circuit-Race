using System;
using UnityEngine;

namespace CircuitRace.Environment
{
    public sealed class GhibliMeadowFoliageData : ScriptableObject
    {
        [Serializable]
        public struct Prototype
        {
            public Mesh mesh;
            public Material material;
            public int subMesh;
            public Matrix4x4 localTransform;
        }

        [Serializable]
        public struct Placement
        {
            public int prototype;
            public Vector3 position;
            public float yaw;
            public Vector3 scale;
        }

        public Prototype[] prototypes = Array.Empty<Prototype>();
        public Placement[] placements = Array.Empty<Placement>();
    }
}
