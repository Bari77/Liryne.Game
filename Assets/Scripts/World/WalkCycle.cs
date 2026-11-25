using UnityEngine;

namespace Liryne.Game.World
{
    /// <summary>Marche, saut, idle (regard, clignement) et animation des monstres.</summary>
    public sealed class WalkCycle : MonoBehaviour
    {
        EntityKindVisual _kind;
        Transform _mover;
        Vector3 _lastPos;
        bool _hasLast;
        float _phase;
        float _weight;

        Transform _spine;
        Transform _head;
        Transform _eyeL;
        Transform _eyeR;
        Transform _legL;
        Transform _kneeL;
        Transform _legR;
        Transform _kneeR;
        Transform _armL;
        Transform _elbowL;
        Transform _wristL;
        Transform _armR;
        Transform _elbowR;
        Transform _wristR;
        Transform _blob;
        Transform _blobTop;
        Transform _wingL;
        Transform _wingR;

        Pose _spinePose;
        Pose _headPose;
        Pose _eyeLPose;
        Pose _eyeRPose;
        Pose _legLPose;
        Pose _kneeLPose;
        Pose _legRPose;
        Pose _kneeRPose;
        Pose _armLPose;
        Pose _elbowLPose;
        Pose _wristLPose;
        Pose _armRPose;
        Pose _elbowRPose;
        Pose _wristRPose;
        Pose _blobPose;
        Pose _blobTopPose;
        Pose _wingLPose;
        Pose _wingRPose;

        float _idleClock;
        Vector2 _look;
        Vector2 _lookTarget;
        Vector2 _saccade;
        Vector2 _saccadeTarget;
        float _glanceIn;
        float _saccadeIn;
        float _blinkIn;
        float _blink;
        float _guard;
        float _strike;
        int _style;

        struct Pose
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public bool Valid;
        }

        public void Bind(EntityKindVisual kind)
        {
            _kind = kind;
            _spine = transform.Find("Spine");
            _head = transform.Find("Spine/Head");
            _eyeL = transform.Find("Spine/Head/EyeL");
            _eyeR = transform.Find("Spine/Head/EyeR");
            _legL = transform.Find("LegL");
            _kneeL = transform.Find("LegL/KneeL");
            _legR = transform.Find("LegR");
            _kneeR = transform.Find("LegR/KneeR");
            _armL = transform.Find("Spine/ArmL");
            _elbowL = transform.Find("Spine/ArmL/ElbowL");
            _wristL = transform.Find("Spine/ArmL/ElbowL/WristL");
            _armR = transform.Find("Spine/ArmR");
            _elbowR = transform.Find("Spine/ArmR/ElbowR");
            _wristR = transform.Find("Spine/ArmR/ElbowR/WristR");
            _blob = transform.Find("Blob");
            _blobTop = transform.Find("BlobTop");
            _wingL = transform.Find("WingL");
            _wingR = transform.Find("WingR");
            _spinePose = Capture(_spine);
            _headPose = Capture(_head);
            _eyeLPose = Capture(_eyeL);
            _eyeRPose = Capture(_eyeR);
            _legLPose = Capture(_legL);
            _kneeLPose = Capture(_kneeL);
            _legRPose = Capture(_legR);
            _kneeRPose = Capture(_kneeR);
            _armLPose = Capture(_armL);
            _elbowLPose = Capture(_elbowL);
            _wristLPose = Capture(_wristL);
            _armRPose = Capture(_armR);
            _elbowRPose = Capture(_elbowR);
            _wristRPose = Capture(_wristR);
            _blobPose = Capture(_blob);
            _blobTopPose = Capture(_blobTop);
            _wingLPose = Capture(_wingL);
            _wingRPose = Capture(_wingR);
            _hasLast = false;
            _phase = Random.value * Mathf.PI * 2f;
            _weight = 0f;
            _idleClock = Random.Range(0f, 12f);
            _look = Vector2.zero;
            _lookTarget = Vector2.zero;
            _saccade = Vector2.zero;
            _saccadeTarget = Vector2.zero;
            _glanceIn = Random.Range(0.4f, 1.6f);
            _saccadeIn = Random.Range(0.3f, 1.1f);
            _blinkIn = Random.Range(1.2f, 3.5f);
            _blink = 0f;
            _guard = 0f;
            _strike = 0f;
            _style = 0;
            if (transform.Find("BowHolster") is not null)
            {
                _style = 1;
            }
            else if (transform.Find("StaffHolster") is not null || transform.Find("OrbHolsterL") is not null)
            {
                _style = 2;
            }
        }

        public void PlayStrike()
        {
            if (_strike > 0.45f || _strike <= 0f)
            {
                _strike = 0.001f;
            }
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            Transform mover = transform.parent != null ? transform.parent : transform;
            if (_mover != mover)
            {
                _mover = mover;
                _hasLast = false;
            }

            Vector3 pos = _mover.position;
            float speed = 0f;
            if (_hasLast)
            {
                float planar = new Vector2(pos.x - _lastPos.x, pos.z - _lastPos.z).magnitude;
                if (planar < 4f)
                {
                    speed = planar / dt;
                }
            }

            _lastPos = pos;
            _hasLast = true;

            var avatar = _mover.GetComponent<EntityAvatar>();
            bool dead = avatar is not null && avatar.IsDead;
            bool combat = avatar is not null && avatar.InCombat && !dead;
            _guard = Mathf.MoveTowards(_guard, combat ? 1f : 0f, dt * 6f);
            if (_strike > 0f)
            {
                _strike += dt / 0.46f;
                if (_strike >= 1f)
                {
                    _strike = 0f;
                }
            }

            float target = dead ? 0f : Mathf.Clamp01(speed / 2.4f);
            _weight = Mathf.MoveTowards(_weight, target, dt * (target > _weight ? 9f : 6f));

            if (_kind == EntityKindVisual.Monster)
            {
                _phase += dt * (2.8f + (speed * 0.45f));
                TickMonster(dead);
                return;
            }

            if (_weight > 0.02f)
            {
                _phase += dt * (6.4f + (speed * 0.28f)) * Mathf.Lerp(0.35f, 1f, _weight);
            }

            if (!dead && avatar is not null && _mover.position.y > 0.14f)
            {
                TickJump();
                return;
            }

            TickIdleBrain(dt, dead);
            TickPlayer(dead);
        }

        void TickIdleBrain(float dt, bool dead)
        {
            _idleClock += dt;
            _glanceIn -= dt;
            _saccadeIn -= dt;
            _blinkIn -= dt;

            if (dead)
            {
                _lookTarget = Vector2.zero;
                _saccadeTarget = Vector2.zero;
                _blink = 0f;
            }
            else
            {
                if (_glanceIn <= 0f)
                {
                    _glanceIn = Random.Range(2.2f, 5.4f);
                    if (Random.value < 0.18f)
                    {
                        _lookTarget = Vector2.zero;
                    }
                    else
                    {
                        _lookTarget = new Vector2(Random.Range(-20f, 20f), Random.Range(-6f, 8f));
                    }
                }

                if (_saccadeIn <= 0f)
                {
                    _saccadeIn = Random.Range(0.35f, 1.4f);
                    _saccadeTarget = new Vector2(Random.Range(-4.5f, 4.5f), Random.Range(-2.5f, 2.5f));
                }

                if (_blinkIn <= 0f)
                {
                    _blink = 1f;
                    _blinkIn = Random.Range(2.6f, 6.2f);
                    if (Random.value < 0.22f)
                    {
                        _blinkIn = 0.14f;
                    }
                }
            }

            float glance = 1f - Mathf.Exp(-3.4f * dt);
            _look = Vector2.Lerp(_look, _lookTarget, glance);
            _saccade = Vector2.Lerp(_saccade, _saccadeTarget, 1f - Mathf.Exp(-14f * dt));
            _blink = Mathf.MoveTowards(_blink, 0f, dt * 11f);
        }

        void TickJump()
        {
            Apply(_legL, _legLPose, Vector3.zero, Quaternion.Euler(-38f, 0f, 0f));
            Apply(_legR, _legRPose, Vector3.zero, Quaternion.Euler(-22f, 0f, 0f));
            Apply(_kneeL, _kneeLPose, Vector3.zero, Quaternion.Euler(48f, 0f, 0f));
            Apply(_kneeR, _kneeRPose, Vector3.zero, Quaternion.Euler(36f, 0f, 0f));
            Vector3 armL = new Vector3(28f, 0f, 4f);
            Vector3 armR = new Vector3(-18f, 0f, -4f);
            Vector3 elL = new Vector3(-28f, 0f, 0f);
            Vector3 elR = new Vector3(-22f, 0f, 0f);
            Vector3 wrL = Vector3.zero;
            Vector3 wrR = Vector3.zero;
            BlendFight(ref armL, ref armR, ref elL, ref elR, ref wrL, ref wrR);
            Apply(_armL, _armLPose, Vector3.zero, Quaternion.Euler(armL));
            Apply(_armR, _armRPose, Vector3.zero, Quaternion.Euler(armR));
            Apply(_elbowL, _elbowLPose, Vector3.zero, Quaternion.Euler(elL));
            Apply(_elbowR, _elbowRPose, Vector3.zero, Quaternion.Euler(elR));
            Apply(_wristL, _wristLPose, Vector3.zero, Quaternion.Euler(wrL));
            Apply(_wristR, _wristRPose, Vector3.zero, Quaternion.Euler(wrR));
            Apply(_spine, _spinePose, new Vector3(0f, 0.02f, 0f), Quaternion.Euler(8f, 0f, 0f));
            Apply(_head, _headPose, Vector3.zero, Quaternion.Euler(-6f, 0f, 0f));
            ResetEyes();
        }

        void TickPlayer(bool dead)
        {
            float swing = Mathf.Sin(_phase);
            float pass = Mathf.Max(0f, Mathf.Cos(_phase));
            float idle = dead ? 0f : (1f - _weight);
            float bob = Mathf.Abs(swing) * 0.038f * _weight;
            float breath = Mathf.Sin(_idleClock * 1.55f) * 0.012f * idle;
            float shift = Mathf.Sin(_idleClock * 0.55f);
            float hip = 30f * _weight;
            float knee = 44f * _weight;
            float arm = 22f * _weight;
            float lookY = _look.x * idle;
            float lookX = -_look.y * idle;

            Apply(
                _legL,
                _legLPose,
                Vector3.zero,
                Quaternion.Euler((-swing * hip) + (shift * 5f * idle), 0f, shift * 2.5f * idle));
            Apply(
                _legR,
                _legRPose,
                Vector3.zero,
                Quaternion.Euler((swing * hip) - (shift * 5f * idle), 0f, -shift * 2.5f * idle));
            Apply(
                _kneeL,
                _kneeLPose,
                Vector3.zero,
                Quaternion.Euler(
                    (Mathf.Max(0f, -swing) * knee) + (pass * 10f * _weight) + (Mathf.Max(0f, shift) * 6f * idle),
                    0f,
                    0f));
            Apply(
                _kneeR,
                _kneeRPose,
                Vector3.zero,
                Quaternion.Euler(
                    (Mathf.Max(0f, swing) * knee)
                    + (Mathf.Max(0f, -Mathf.Cos(_phase)) * 10f * _weight)
                    + (Mathf.Max(0f, -shift) * 6f * idle),
                    0f,
                    0f));
            float walkArm = 1f - (0.7f * _guard);
            Vector3 armL = new Vector3(
                (swing * arm * walkArm) + (Mathf.Sin(_idleClock * 1.15f) * 4f * idle),
                0f,
                -2f * _weight);
            Vector3 armR = new Vector3(
                (-swing * arm * walkArm) + (Mathf.Sin((_idleClock * 1.15f) + 0.8f) * 4f * idle),
                0f,
                2f * _weight);
            Vector3 elL = new Vector3(-(6f + (12f * _weight) + (Mathf.Max(0f, swing) * 12f * _weight) + (3f * idle)), 0f, -2f);
            Vector3 elR = new Vector3(-(6f + (12f * _weight) + (Mathf.Max(0f, -swing) * 12f * _weight) + (3f * idle)), 0f, 2f);
            Vector3 wrL = Vector3.zero;
            Vector3 wrR = Vector3.zero;
            BlendFight(ref armL, ref armR, ref elL, ref elR, ref wrL, ref wrR);

            Apply(_armL, _armLPose, Vector3.zero, Quaternion.Euler(armL));
            Apply(_armR, _armRPose, Vector3.zero, Quaternion.Euler(armR));
            Apply(_elbowL, _elbowLPose, Vector3.zero, Quaternion.Euler(elL));
            Apply(_elbowR, _elbowRPose, Vector3.zero, Quaternion.Euler(elR));
            Apply(_wristL, _wristLPose, Vector3.zero, Quaternion.Euler(wrL));
            Apply(_wristR, _wristRPose, Vector3.zero, Quaternion.Euler(wrR));

            float collapse = dead ? 18f : 0f;
            float cutTwist = 0f;
            if (_strike > 0.28f && _strike < 0.78f)
            {
                float peak = _strike < 0.52f
                    ? Smooth((_strike - 0.28f) / 0.24f)
                    : Smooth(1f - ((_strike - 0.52f) / 0.26f));
                cutTwist = -18f * peak;
            }

            Apply(
                _spine,
                _spinePose,
                new Vector3(shift * 0.012f * idle, bob + breath, 0f),
                Quaternion.Euler(
                    (-6f * _weight) + collapse + (lookX * 0.28f) + (6f * _guard),
                    (swing * 7f * _weight) + (lookY * 0.42f) + cutTwist,
                    shift * 3.2f * idle));
            Apply(
                _head,
                _headPose,
                Vector3.zero,
                Quaternion.Euler(lookX + (Mathf.Sin(_idleClock * 0.7f) * 1.4f * idle), lookY, lookY * 0.08f));
            ApplyEyes(idle);
        }

        void BlendFight(ref Vector3 armL, ref Vector3 armR, ref Vector3 elL, ref Vector3 elR, ref Vector3 wrL, ref Vector3 wrR)
        {
            if (_guard <= 0.01f && _strike <= 0f)
            {
                return;
            }

            Vector3 gArmL;
            Vector3 gArmR;
            Vector3 gElL;
            Vector3 gElR;
            Vector3 gWrL;
            Vector3 gWrR;
            GuardPose(out gArmL, out gArmR, out gElL, out gElR, out gWrL, out gWrR);
            if (_strike > 0f)
            {
                StrikePose(_strike, ref gArmL, ref gArmR, ref gElL, ref gElR, ref gWrL, ref gWrR);
            }

            float w = Mathf.Max(_guard, _strike > 0f ? 1f : 0f);
            armL = Vector3.Lerp(armL, gArmL, w);
            armR = Vector3.Lerp(armR, gArmR, w);
            elL = Vector3.Lerp(elL, gElL, w);
            elR = Vector3.Lerp(elR, gElR, w);
            wrL = Vector3.Lerp(wrL, gWrL, w);
            wrR = Vector3.Lerp(wrR, gWrR, w);
        }

        void GuardPose(out Vector3 armL, out Vector3 armR, out Vector3 elL, out Vector3 elR, out Vector3 wrL, out Vector3 wrR)
        {
            if (_style == 1)
            {
                armL = new Vector3(-42f, 18f, 4f);
                armR = new Vector3(-8f, 22f, 6f);
                elL = new Vector3(-28f, 0f, 4f);
                elR = new Vector3(-55f, 0f, -4f);
                wrL = new Vector3(8f, 0f, 0f);
                wrR = new Vector3(-12f, 0f, 0f);
                return;
            }

            if (_style == 2)
            {
                armL = new Vector3(-18f, 10f, 4f);
                armR = new Vector3(-28f, -8f, 4f);
                elL = new Vector3(-22f, 0f, 2f);
                elR = new Vector3(-38f, 0f, -2f);
                wrL = Vector3.zero;
                wrR = new Vector3(-8f, 0f, 0f);
                return;
            }

            armL = new Vector3(-52f, 28f, 6f);
            armR = new Vector3(-18f, 32f, 4f);
            elL = new Vector3(-58f, 8f, 6f);
            elR = new Vector3(-42f, -6f, -4f);
            wrL = new Vector3(12f, 0f, -8f);
            wrR = new Vector3(-18f, 10f, 12f);
        }

        void StrikePose(float u, ref Vector3 armL, ref Vector3 armR, ref Vector3 elL, ref Vector3 elR, ref Vector3 wrL, ref Vector3 wrR)
        {
            Vector3 upArmR;
            Vector3 downArmR;
            Vector3 upElR;
            Vector3 downElR;
            Vector3 upWrR;
            Vector3 downWrR;
            Vector3 braceArmL = armL + new Vector3(-10f, -6f, 0f);
            Vector3 braceElL = elL + new Vector3(-8f, 0f, 0f);

            if (_style == 1)
            {
                upArmR = new Vector3(-8f, 38f, 8f);
                downArmR = new Vector3(-36f, 6f, 4f);
                upElR = new Vector3(-72f, 0f, -6f);
                downElR = new Vector3(-18f, 0f, 0f);
                upWrR = new Vector3(-20f, 0f, 0f);
                downWrR = new Vector3(8f, 0f, 0f);
                braceArmL = new Vector3(-50f, 16f, 4f);
            }
            else if (_style == 2)
            {
                upArmR = new Vector3(-70f, -12f, 6f);
                downArmR = new Vector3(-22f, 16f, 4f);
                upElR = new Vector3(-20f, 0f, 0f);
                downElR = new Vector3(-48f, 0f, 0f);
                upWrR = new Vector3(12f, 0f, 0f);
                downWrR = new Vector3(-16f, 0f, 0f);
                braceArmL = new Vector3(-40f, 8f, 4f);
            }
            else
            {
                upArmR = new Vector3(-78f, 48f, 8f);
                downArmR = new Vector3(22f, -38f, -6f);
                upElR = new Vector3(-28f, -8f, -6f);
                downElR = new Vector3(-58f, 12f, 4f);
                upWrR = new Vector3(-8f, 16f, 18f);
                downWrR = new Vector3(16f, -22f, -8f);
            }

            Vector3 fromArmR;
            Vector3 toArmR;
            Vector3 fromElR;
            Vector3 toElR;
            Vector3 fromWrR;
            Vector3 toWrR;
            float k;
            if (u < 0.3f)
            {
                k = Smooth(u / 0.3f);
                fromArmR = armR;
                toArmR = upArmR;
                fromElR = elR;
                toElR = upElR;
                fromWrR = wrR;
                toWrR = upWrR;
            }
            else if (u < 0.52f)
            {
                k = Smooth((u - 0.3f) / 0.22f);
                fromArmR = upArmR;
                toArmR = downArmR;
                fromElR = upElR;
                toElR = downElR;
                fromWrR = upWrR;
                toWrR = downWrR;
                armL = Vector3.Lerp(armL, braceArmL, k);
                elL = Vector3.Lerp(elL, braceElL, k);
            }
            else
            {
                k = Smooth((u - 0.52f) / 0.48f);
                fromArmR = downArmR;
                toArmR = armR;
                fromElR = downElR;
                toElR = elR;
                fromWrR = downWrR;
                toWrR = wrR;
                armL = Vector3.Lerp(braceArmL, armL, k);
                elL = Vector3.Lerp(braceElL, elL, k);
            }

            armR = Vector3.Lerp(fromArmR, toArmR, k);
            elR = Vector3.Lerp(fromElR, toElR, k);
            wrR = Vector3.Lerp(fromWrR, toWrR, k);
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - (2f * t));
        }

        void ApplyEyes(float idle)
        {
            float shut = _blink * idle;
            Vector3 squint = new Vector3(1f, Mathf.Lerp(1f, 0.08f, shut), 1f);
            float dartX = (_look.x * 0.55f) + (_saccade.x * idle);
            float dartY = (-_look.y * 0.4f) + (_saccade.y * idle);
            ApplyScale(
                _eyeL,
                _eyeLPose,
                Quaternion.Euler(dartY, dartX, 0f),
                squint);
            ApplyScale(
                _eyeR,
                _eyeRPose,
                Quaternion.Euler(dartY, dartX, 0f),
                squint);
        }

        void ResetEyes()
        {
            ApplyScale(_eyeL, _eyeLPose, Quaternion.identity, Vector3.one);
            ApplyScale(_eyeR, _eyeRPose, Quaternion.identity, Vector3.one);
        }

        void TickMonster(bool dead)
        {
            float hop = Mathf.Abs(Mathf.Sin(_phase));
            float squash = (hop - 0.5f) * 0.18f * _weight;
            float lift = hop * 0.14f * _weight;
            float breath = Mathf.Sin(Time.time * 2.2f) * 0.03f * (1f - _weight);

            if (_blobPose.Valid)
            {
                Vector3 scale = _blobPose.Scale;
                scale.y *= 1f + squash + breath;
                scale.x *= 1f - (squash * 0.55f);
                scale.z *= 1f - (squash * 0.55f);
                if (dead)
                {
                    scale.y *= 0.45f;
                    scale.x *= 1.25f;
                    scale.z *= 1.25f;
                    lift = 0f;
                }

                _blob.localPosition = _blobPose.Position + new Vector3(0f, lift, 0f);
                _blob.localScale = scale;
            }

            if (_blobTopPose.Valid)
            {
                Vector3 scale = _blobTopPose.Scale;
                scale.y *= 1f + (squash * 0.7f) + (breath * 0.6f);
                _blobTop.localPosition = _blobTopPose.Position + new Vector3(0f, lift * 1.2f, 0f);
                _blobTop.localScale = scale;
            }

            float flap = Mathf.Sin(_phase * 3.1f) * Mathf.Lerp(18f, 42f, _weight);
            if (dead)
            {
                flap = -28f;
            }

            Apply(_wingL, _wingLPose, Vector3.zero, Quaternion.Euler(flap, 0f, flap * 0.35f));
            Apply(_wingR, _wingRPose, Vector3.zero, Quaternion.Euler(flap, 0f, -flap * 0.35f));
        }

        static Pose Capture(Transform joint)
        {
            if (joint is null)
            {
                return default;
            }

            return new Pose
            {
                Position = joint.localPosition,
                Rotation = joint.localRotation,
                Scale = joint.localScale,
                Valid = true,
            };
        }

        static void Apply(Transform joint, Pose rest, Vector3 offset, Quaternion swing)
        {
            if (joint is null || !rest.Valid)
            {
                return;
            }

            joint.localPosition = rest.Position + offset;
            joint.localRotation = rest.Rotation * swing;
        }

        static void ApplyScale(Transform joint, Pose rest, Quaternion swing, Vector3 scaleMul)
        {
            if (joint is null || !rest.Valid)
            {
                return;
            }

            joint.localRotation = rest.Rotation * swing;
            joint.localScale = Vector3.Scale(rest.Scale, scaleMul);
        }
    }
}
