using System.Collections.Generic;
using Kapicua.Core;
using UnityEngine;

namespace Kapicua.Game
{
    /// <summary>A row of face-down tiles showing only how many tiles a player still holds.</summary>
    public class OpponentHandView : MonoBehaviour
    {
        public float Spacing = 1.05f;

        readonly List<GameObject> _views = new List<GameObject>();

        public void Refresh(int count)
        {
            foreach (var v in _views)
                if (v != null) Destroy(v);
            _views.Clear();

            float x0 = -(count - 1) * Spacing / 2f;
            for (int i = 0; i < count; i++)
            {
                // Face-down dummy: never reveals the real tile.
                var view = TileView.Create(new DominoTile(0, 0), faceUp: false);
                view.transform.SetParent(transform, false);
                view.transform.localPosition = new Vector3(x0 + i * Spacing, 0, 0);
                _views.Add(view.gameObject);
            }
        }
    }
}
