using System.Collections.Generic;
using UnityEngine;

namespace Liryne.Game.World
{
    /// <summary>Rejoue les MoveIntent plus récents que LastProcessedMoveSequence.</summary>
    public sealed class MovePredictor
    {
        readonly List<Pending> _pending = new List<Pending>();

        public void Record(uint sequence, float dirX, float dirZ)
        {
            _pending.Add(new Pending(sequence, dirX, dirZ));
        }

        public Vector3 Reconcile(Vector3 serverPosition, float moveSpeed, uint lastProcessed, float tickDelta)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].Sequence <= lastProcessed)
                {
                    _pending.RemoveAt(i);
                }
            }

            Vector3 position = serverPosition;
            for (int i = 0; i < _pending.Count; i++)
            {
                Pending move = _pending[i];
                position = VillageCollision.MovePlanar(position, move.DirX, move.DirZ, moveSpeed, tickDelta);
            }

            return position;
        }

        public void Clear() => _pending.Clear();

        readonly struct Pending
        {
            public Pending(uint sequence, float dirX, float dirZ)
            {
                Sequence = sequence;
                DirX = dirX;
                DirZ = dirZ;
            }

            public uint Sequence { get; }
            public float DirX { get; }
            public float DirZ { get; }
        }
    }
}
