using System.Collections.Generic;
using Kapicua.Core;
using UnityEngine;

namespace Kapicua.Game
{
    /// <summary>
    /// Lays the chain out on the table. The first tile sits at the local origin and the
    /// chain grows as two arms (left/right). Each arm runs along X and serpentines into
    /// a new row (right arm toward +Z, left arm toward -Z) when it reaches the edge.
    /// Doubles are placed crosswise and take up their width along the path.
    /// </summary>
    public class BoardLayoutView : MonoBehaviour
    {
        public float HalfWidth = 7.2f;
        public float RowSpacing = 1.5f;

        class Arm
        {
            public Vector3 Cursor;
            public Vector3 Dir;
            public int RowSign;
        }

        Arm _left, _right;
        readonly List<GameObject> _spawned = new List<GameObject>();

        public void Clear()
        {
            foreach (var go in _spawned)
                if (go != null) Destroy(go);
            _spawned.Clear();
            _left = _right = null;
        }

        public void PlaceFirst(PlacedTile pt)
        {
            float len = PathLength(pt.Tile);
            Spawn(pt.Tile, Vector3.zero, Vector3.right, pt.LeftValue);
            _right = new Arm { Cursor = Vector3.right * (len / 2f), Dir = Vector3.right, RowSign = 1 };
            _left = new Arm { Cursor = Vector3.left * (len / 2f), Dir = Vector3.left, RowSign = -1 };
        }

        public void Append(PlacedTile pt, BoardEnd end)
        {
            var arm = end == BoardEnd.Left ? _left : _right;
            float len = PathLength(pt.Tile);

            Vector3 reach = arm.Cursor + arm.Dir * len;
            if (Mathf.Abs(reach.x) > HalfWidth)
            {
                arm.Cursor += new Vector3(0, 0, RowSpacing * arm.RowSign);
                arm.Dir = -arm.Dir;
            }

            // The half matching the previous end must face back along the arm (-Dir = local -Z).
            int negZValue = end == BoardEnd.Right ? pt.LeftValue : pt.RightValue;
            Spawn(pt.Tile, arm.Cursor + arm.Dir * (len / 2f), arm.Dir, negZValue);
            arm.Cursor += arm.Dir * len;
        }

        static float PathLength(DominoTile tile) => tile.IsDouble ? TileView.Width : TileView.Length;

        void Spawn(DominoTile tile, Vector3 center, Vector3 dir, int negZValue)
        {
            var view = TileView.Create(tile, true, negZValue);
            var t = view.transform;
            t.SetParent(transform, false);
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            if (tile.IsDouble) rot *= Quaternion.Euler(0, 90, 0);
            t.localRotation = rot;
            t.localPosition = center + Vector3.up * (TileView.Thickness / 2f);
            _spawned.Add(view.gameObject);
        }
    }
}
