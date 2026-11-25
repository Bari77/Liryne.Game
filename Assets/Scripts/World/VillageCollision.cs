using UnityEngine;

namespace Liryne.Game.World
{
    /// <summary>
    /// Collision et saut de la Clairière. Mêmes volumes et constantes que SliceObstacles / Locomotion.
    /// </summary>
    public static class VillageCollision
    {
        public const float Radius = 0.38f;
        public const float StepHeight = 0.45f;
        public const float Gravity = 24f;
        public const float JumpSpeed = 8.2f;
        public const float GroundEpsilon = 0.02f;

        static readonly Obstacle[] Obstacles = Build();

        public static bool IsGrounded(float y, float verticalVelocity) =>
            y <= GroundEpsilon && verticalVelocity <= 0.01f;

        public static void Integrate(ref Vector3 position, ref float verticalVelocity, float dirX, float dirZ, float speed, bool jump, float dt)
        {
            if (jump && IsGrounded(position.y, verticalVelocity))
            {
                verticalVelocity = JumpSpeed;
            }

            var planar = new Vector2(dirX, dirZ);
            if (planar.sqrMagnitude > 0.0001f)
            {
                planar.Normalize();
                Vector3 from = position;
                position.x += planar.x * speed * dt;
                position.z += planar.y * speed * dt;
                position = Slide(from, position);
            }

            verticalVelocity -= Gravity * dt;
            position.y += verticalVelocity * dt;
            if (position.y <= 0f)
            {
                position.y = 0f;
                verticalVelocity = 0f;
            }

            position.x = Mathf.Clamp(position.x, -60f, 60f);
            position.z = Mathf.Clamp(position.z, -60f, 60f);
        }

        public static Vector3 MovePlanar(Vector3 position, float dirX, float dirZ, float speed, float dt)
        {
            var planar = new Vector2(dirX, dirZ);
            if (planar.sqrMagnitude <= 0.0001f)
            {
                return position;
            }

            planar.Normalize();
            Vector3 from = position;
            position.x += planar.x * speed * dt;
            position.z += planar.y * speed * dt;
            position = Slide(from, position);
            position.x = Mathf.Clamp(position.x, -60f, 60f);
            position.z = Mathf.Clamp(position.z, -60f, 60f);
            return position;
        }

        static Vector3 Slide(Vector3 from, Vector3 to)
        {
            if (!Overlaps(to))
            {
                return to;
            }

            var xOnly = new Vector3(to.x, to.y, from.z);
            var zOnly = new Vector3(from.x, to.y, to.z);
            bool xFree = !Overlaps(xOnly);
            bool zFree = !Overlaps(zOnly);
            if (xFree && !zFree)
            {
                return xOnly;
            }

            if (zFree && !xFree)
            {
                return zOnly;
            }

            if (xFree && zFree)
            {
                return Mathf.Abs(to.x - from.x) >= Mathf.Abs(to.z - from.z) ? xOnly : zOnly;
            }

            return from;
        }

        static bool Overlaps(Vector3 position)
        {
            for (int i = 0; i < Obstacles.Length; i++)
            {
                Obstacle obstacle = Obstacles[i];
                if (obstacle.Height <= position.y + StepHeight)
                {
                    continue;
                }

                if (Hits(position, obstacle))
                {
                    return true;
                }
            }

            return false;
        }

        static bool Hits(Vector3 position, Obstacle obstacle)
        {
            float dx = position.x - obstacle.X;
            float dz = position.z - obstacle.Z;
            if (obstacle.Radius > 0f)
            {
                float reach = obstacle.Radius + Radius;
                return (dx * dx) + (dz * dz) < reach * reach;
            }

            float rad = obstacle.Yaw * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            float localX = (cos * dx) + (sin * dz);
            float localZ = (-sin * dx) + (cos * dz);
            float closestX = Mathf.Clamp(localX, -obstacle.HalfX, obstacle.HalfX);
            float closestZ = Mathf.Clamp(localZ, -obstacle.HalfZ, obstacle.HalfZ);
            float ex = localX - closestX;
            float ez = localZ - closestZ;
            return (ex * ex) + (ez * ez) < Radius * Radius;
        }

        static Obstacle[] Build()
        {
            var list = new System.Collections.Generic.List<Obstacle>(80)
            {
                Box(-21f, 1f, 90f, 8.5f, 6.2f, 3.4f),
                Box(6f, 20f, 180f, 5.2f, 4.6f, 2.6f),
                Box(20f, 15f, -130f, 4.6f, 4.2f, 2.4f),
                Box(19f, -16f, -40f, 5f, 4.4f, 2.5f),
                Box(-4f, -21f, 0f, 4.8f, 4.2f, 2.3f),
                Box(-19f, -17f, 40f, 4.4f, 4f, 2.4f),
                Box(-17f, 18f, 140f, 4.6f, 4.2f, 2.5f),
                Cylinder(4.2f, -4.8f, 1.15f, 0.85f),
                Cylinder(-3.6f, 3.8f, 0.9f, 1.85f),
                Box(13.5f, 1.5f, -90f, 3.4f, 1.6f, 0.85f),
                Cylinder(-1.2f, 6.5f, 0.22f, 2f),
                Box(3.4f, -1.1f, 18f, 0.9f, 0.9f, 0.72f),
                Box(4.4f, -1.4f, -12f, 0.8f, 0.8f, 0.58f),
            };

            const float fence = 27f;
            for (int i = 0; i < 36; i++)
            {
                float a = i * 10f * Mathf.Deg2Rad;
                var pos = new Vector2(Mathf.Sin(a) * fence, Mathf.Cos(a) * fence);
                if (Mathf.Abs(pos.x) < 3.5f || Mathf.Abs(pos.y) < 3.5f)
                {
                    continue;
                }

                list.Add(Cylinder(pos.x, pos.y, 0.22f, 1.1f));
            }

            Vector2[] trees =
            {
                new Vector2(28f, 10f), new Vector2(26f, -12f), new Vector2(24f, 24f),
                new Vector2(-27f, 8f), new Vector2(-25f, -10f), new Vector2(-24f, 26f),
                new Vector2(10f, 28f), new Vector2(-8f, 27f), new Vector2(12f, -28f),
                new Vector2(-12f, -27f), new Vector2(32f, 2f), new Vector2(-32f, -4f),
                new Vector2(8f, 32f), new Vector2(-30f, 18f), new Vector2(29f, -22f),
            };
            for (int i = 0; i < trees.Length; i++)
            {
                float scale = 1f + (i % 4) * 0.12f;
                list.Add(Cylinder(trees[i].x, trees[i].y, 0.32f * scale, 2.4f * scale));
            }

            Vector2[] planters =
            {
                new Vector2(2.8f, 7.2f), new Vector2(-6.5f, 5f), new Vector2(7.4f, -2.2f),
                new Vector2(-7.2f, -6f), new Vector2(5.5f, 6.2f), new Vector2(-5f, 7.4f),
            };
            for (int i = 0; i < planters.Length; i++)
            {
                list.Add(Box(planters[i].x, planters[i].y, 0f, 1.1f, 0.7f, 0.35f));
            }

            return list.ToArray();
        }

        static Obstacle Box(float x, float z, float yaw, float width, float depth, float height) =>
            new Obstacle(x, z, yaw, width * 0.5f, depth * 0.5f, height, 0f);

        static Obstacle Cylinder(float x, float z, float radius, float height) =>
            new Obstacle(x, z, 0f, 0f, 0f, height, radius);

        readonly struct Obstacle
        {
            public Obstacle(float x, float z, float yaw, float halfX, float halfZ, float height, float radius)
            {
                X = x;
                Z = z;
                Yaw = yaw;
                HalfX = halfX;
                HalfZ = halfZ;
                Height = height;
                Radius = radius;
            }

            public float X { get; }
            public float Z { get; }
            public float Yaw { get; }
            public float HalfX { get; }
            public float HalfZ { get; }
            public float Height { get; }
            public float Radius { get; }
        }
    }
}
