using System;
using System.Collections.Generic;
using Liryne.Protocol.Messages.Combat;
using Liryne.Protocol.Messages.Entities;
using Liryne.Protocol.Wire;
using UnityEngine;

namespace Liryne.Game.World
{
    /// <summary>Clairière : village, avatars, caméra derrière le joueur.</summary>
    public sealed class WorldStage
    {
        readonly Transform _root;
        readonly Dictionary<Guid, EntityAvatar> _avatars = new Dictionary<Guid, EntityAvatar>();
        readonly MovePredictor _predictor = new MovePredictor();
        readonly GameObject _ground;
        readonly List<EntityAvatar> _targetCycle = new List<EntityAvatar>();
        Camera _camera;
        EntityAvatar _local;
        float _orbitYaw = 210f;
        float _followYaw = 180f;
        float _followPitch = 22f;
        float _followDistance = 12.5f;
        const float ZoomMin = 4f;
        const float ZoomMax = 20f;
        Guid _selectedId;
        float _verticalVelocity;

        public Guid LocalEntityId { get; private set; }
        public EntityAvatar Local => _local;
        public IReadOnlyDictionary<Guid, EntityAvatar> Avatars => _avatars;
        public float TickDelta { get; set; } = 0.05f;

        public WorldStage(Transform parent)
        {
            var rootGo = new GameObject("WorldStage");
            rootGo.transform.SetParent(parent, false);
            _root = rootGo.transform;

            _ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _ground.name = "Ground";
            _ground.transform.SetParent(_root, false);
            _ground.transform.localScale = new Vector3(16f, 1f, 16f);
            PaintGround(_ground);

            EnsureCamera();
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            _root.gameObject.SetActive(visible);
            if (visible)
            {
                EnsureCamera();
                if (_camera is not null)
                {
                    _camera.enabled = true;
                }
            }
        }

        public void Enter(Guid localEntityId, Vec3 position)
        {
            LocalEntityId = localEntityId;
            _predictor.Clear();
            _verticalVelocity = 0f;
            EnsureCamera();
            SetVisible(true);
            Vector3 pos = EntityAvatar.ToUnity(position);
            PlaceFollowCamera(pos);
        }

        public void Spawn(EntitySpawn spawn)
        {
            if (_avatars.TryGetValue(spawn.State.EntityId, out EntityAvatar existing))
            {
                UnityEngine.Object.Destroy(existing.gameObject);
                _avatars.Remove(spawn.State.EntityId);
            }

            bool local = spawn.State.EntityId == LocalEntityId;
            EntityAvatar avatar = EntityAvatar.Spawn(spawn, _root, _camera, local);
            _avatars[spawn.State.EntityId] = avatar;
            if (local)
            {
                _local = avatar;
            }
        }

        public void Despawn(Guid entityId)
        {
            if (!_avatars.TryGetValue(entityId, out EntityAvatar avatar))
            {
                return;
            }

            if (_local == avatar)
            {
                _local = null;
            }

            if (_selectedId == entityId)
            {
                _selectedId = Guid.Empty;
            }

            UnityEngine.Object.Destroy(avatar.gameObject);
            _avatars.Remove(entityId);
        }

        public void ApplyPresentation(EntityPresentation presentation)
        {
            if (_avatars.TryGetValue(presentation.EntityId, out EntityAvatar avatar))
            {
                avatar.ApplyPresentation(presentation);
            }
        }

        public void ApplySnapshot(EntitySnapshot snapshot)
        {
            foreach (EntityState state in snapshot.Entities)
            {
                if (!_avatars.TryGetValue(state.EntityId, out EntityAvatar avatar))
                {
                    continue;
                }

                if (avatar.IsLocal)
                {
                    Vector3 server = EntityAvatar.ToUnity(state.Position);
                    Vector3 predicted = _predictor.Reconcile(server, state.MoveSpeed, snapshot.LastProcessedMoveSequence, TickDelta);
                    if ((state.Flags & EntityFlags.Teleported) != 0)
                    {
                        predicted = server;
                        _verticalVelocity = 0f;
                    }
                    else
                    {
                        if (server.y <= 0.05f && avatar.transform.position.y > 0.15f)
                        {
                            predicted.y = avatar.transform.position.y;
                        }

                        float error = Vector3.Distance(avatar.transform.position, predicted);
                        if (error < 2.5f)
                        {
                            predicted = Vector3.Lerp(avatar.transform.position, predicted, 0.45f);
                        }

                        if (predicted.y <= VillageCollision.GroundEpsilon)
                        {
                            _verticalVelocity = 0f;
                        }
                    }

                    avatar.ApplyState(state, TickDelta, snap: true);
                    avatar.SnapTo(predicted, state.Facing);
                    continue;
                }

                avatar.ApplyState(state, TickDelta, snap: false);
            }
        }

        public void RecordMove(uint sequence, float dirX, float dirZ) => _predictor.Record(sequence, dirX, dirZ);

        public void PredictLocal(float dirX, float dirZ, bool jump, float dt)
        {
            if (_local is null)
            {
                return;
            }

            Vector3 next = _local.transform.position;
            if (_local.IsDead)
            {
                dirX = 0f;
                dirZ = 0f;
                jump = false;
            }

            float speed = _local.LastState?.MoveSpeed ?? 6f;
            VillageCollision.Integrate(ref next, ref _verticalVelocity, dirX, dirZ, speed, jump, dt);
            Vector2 dir = new Vector2(dirX, dirZ);
            float facing = _local.transform.eulerAngles.y;
            if (dir.sqrMagnitude > 0.0001f)
            {
                facing = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            }

            _local.SnapTo(next, facing);
        }

        public void PlayStrike(Guid entityId)
        {
            if (_avatars.TryGetValue(entityId, out EntityAvatar avatar))
            {
                avatar.PlayStrike();
            }
        }

        public void PlayCombat(CombatEvents batch)
        {
            foreach (CombatEvent ev in batch.Events)
            {
                if (!_avatars.TryGetValue(ev.TargetEntityId, out EntityAvatar avatar))
                {
                    continue;
                }

                avatar.ShowCombatFloat(ev);
            }
        }

        public void Clear()
        {
            foreach (EntityAvatar avatar in _avatars.Values)
            {
                if (avatar is not null)
                {
                    UnityEngine.Object.Destroy(avatar.gameObject);
                }
            }

            _avatars.Clear();
            _local = null;
            LocalEntityId = Guid.Empty;
            _selectedId = Guid.Empty;
            _predictor.Clear();
            _verticalVelocity = 0f;
        }

        public Vector2 ToCameraMove(float x, float z)
        {
            if (_camera is null)
            {
                return new Vector2(x, z);
            }

            Vector3 forward = _camera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            Vector3 right = _camera.transform.right;
            right.y = 0f;
            right.Normalize();
            Vector3 world = (right * x) + (forward * z);
            var dir = new Vector2(world.x, world.z);
            return dir.sqrMagnitude > 1f ? dir.normalized : dir;
        }

        public EntityAvatar CycleTarget()
        {
            _targetCycle.Clear();
            Vector3 origin = _local is not null ? _local.transform.position : Vector3.zero;
            foreach (EntityAvatar avatar in _avatars.Values)
            {
                if (avatar is null || avatar.IsLocal || avatar.Kind != EntityKind.Monster || avatar.IsDead)
                {
                    continue;
                }

                _targetCycle.Add(avatar);
            }

            _targetCycle.Sort((a, b) =>
            {
                float da = (a.transform.position - origin).sqrMagnitude;
                float db = (b.transform.position - origin).sqrMagnitude;
                return da.CompareTo(db);
            });

            if (_targetCycle.Count == 0)
            {
                SetSelected(Guid.Empty);
                return null;
            }

            int index = -1;
            for (int i = 0; i < _targetCycle.Count; i++)
            {
                if (_targetCycle[i].EntityId == _selectedId)
                {
                    index = i;
                    break;
                }
            }

            EntityAvatar next = _targetCycle[(index + 1) % _targetCycle.Count];
            SetSelected(next.EntityId);
            return next;
        }

        public EntityAvatar Selected =>
            _avatars.TryGetValue(_selectedId, out EntityAvatar avatar) ? avatar : null;

        public void SetSelectedAttacking(bool attacking)
        {
            if (_avatars.TryGetValue(_selectedId, out EntityAvatar avatar))
            {
                avatar.SetSelected(true, attacking);
            }
        }

        void SetSelected(Guid id)
        {
            if (_avatars.TryGetValue(_selectedId, out EntityAvatar previous))
            {
                previous.SetSelected(false);
            }

            _selectedId = id;
            if (_avatars.TryGetValue(_selectedId, out EntityAvatar next))
            {
                next.SetSelected(true);
            }
        }

        public void OrbitLobby(float dt)
        {
            EnsureCamera();
            if (_camera is null)
            {
                return;
            }

            _orbitYaw += dt * 7.5f;
            Vector3 focus = new Vector3(0f, 1.3f, 0f);
            float rad = _orbitYaw * Mathf.Deg2Rad;
            Vector3 pos = focus + new Vector3(Mathf.Sin(rad) * 24f, 12f, Mathf.Cos(rad) * 24f);
            _camera.transform.position = pos;
            WorldLook.Towards(_camera.transform, focus);
        }

        public void OrbitFollow(float yawDelta, float pitchDelta)
        {
            _followYaw = Mathf.Repeat(_followYaw + yawDelta, 360f);
            _followPitch = Mathf.Clamp(_followPitch - pitchDelta, 8f, 62f);
        }

        public void ZoomFollow(float delta)
        {
            _followDistance = Mathf.Clamp(_followDistance + delta, ZoomMin, ZoomMax);
        }

        public void FollowCamera()
        {
            if (_local is null)
            {
                return;
            }

            PlaceFollowCamera(_local.transform.position);
        }

        void PlaceFollowCamera(Vector3 player)
        {
            EnsureCamera();
            if (_camera is null)
            {
                return;
            }

            Vector3 focus = player + Vector3.up * 1.45f;
            Quaternion orbit = Quaternion.Euler(_followPitch, _followYaw, 0f);
            Vector3 desired = focus + (orbit * new Vector3(0f, 0f, -_followDistance));
            _camera.transform.position = Vector3.Lerp(_camera.transform.position, desired, 1f - Mathf.Exp(-10f * Time.deltaTime));
            WorldLook.Towards(_camera.transform, focus);
        }

        void EnsureCamera()
        {
            _camera = Camera.main;
            if (_camera is null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                _camera = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            _camera.clearFlags = CameraClearFlags.Skybox;
            _camera.fieldOfView = 50f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 250f;
            int hidden = (1 << 31) | (1 << 30);
            _camera.cullingMask = ~hidden;
        }

        static void PaintGround(GameObject ground)
        {
            var renderer = ground.GetComponent<MeshRenderer>();
            if (renderer is null)
            {
                return;
            }

            SurfaceArt.Apply(renderer, new Color(0.27f, 0.46f, 0.30f), SurfaceKind.Grass, 0.18f, false, new Vector2(8f, 8f));
        }
    }
}
