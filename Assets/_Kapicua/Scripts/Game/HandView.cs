using System.Collections.Generic;
using Kapicua.Core;
using UnityEngine;

namespace Kapicua.Game
{
    /// <summary>The human player's hand: a tappable row of face-up tiles under the anchor transform.</summary>
    public class HandView : MonoBehaviour
    {
        public float Spacing = 1.15f;

        readonly List<TileView> _views = new List<TileView>();

        public bool Owns(TileView view) => _views.Contains(view);

        public void Refresh(IReadOnlyList<DominoTile> hand, HashSet<DominoTile> playable, DominoTile? selected, bool interactive)
        {
            foreach (var v in _views)
                if (v != null) Destroy(v.gameObject);
            _views.Clear();

            float x0 = -(hand.Count - 1) * Spacing / 2f;
            for (int i = 0; i < hand.Count; i++)
            {
                var tile = hand[i];
                var view = TileView.Create(tile, true);
                view.transform.SetParent(transform, false);

                bool isPlayable = playable.Contains(tile);
                bool isSelected = selected.HasValue && selected.Value == tile;
                float lift = isSelected ? 0.5f : (interactive && isPlayable ? 0.22f : 0f);
                view.transform.localPosition = new Vector3(x0 + i * Spacing, lift, 0);

                if (isSelected) view.SetTint(new Color(1f, 0.95f, 0.6f));
                else if (interactive && !isPlayable) view.SetTint(new Color(0.6f, 0.58f, 0.55f));

                _views.Add(view);
            }
        }
    }
}
