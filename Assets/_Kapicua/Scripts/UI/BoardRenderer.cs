using System;
using Kapicua.Core;
using Kapicua.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kapicua.UI
{
    /// <summary>
    /// Bridges game events to the 3-D domino chain on the table.
    ///
    /// Delegates the actual tile layout to <see cref="BoardLayoutView"/> (which
    /// spawns TileView primitives with real pips, serpentines rows, and lays
    /// doubles crosswise). This component tracks the two open ends — mirroring
    /// GameBoard's logic — so each incoming tile is oriented correctly.
    ///
    /// When a tile fits BOTH open ends, HighlightValidEnds shows two on-screen
    /// buttons; the player's choice is raised through OnEndChosen (GameUIManager
    /// subscribes and calls PlaySelectedTile).
    /// </summary>
    public class BoardRenderer : MonoBehaviour
    {
        [Header("3-D chain layout (world tiles on the table)")]
        public BoardLayoutView LayoutView;

        [Header("End-choice buttons (shown when both ends are valid)")]
        public Button LeftEndButton;
        public Button RightEndButton;

        /// <summary>Raised when the player picks an end for a two-end tile.</summary>
        public event Action<BoardEnd> OnEndChosen;

        int _leftEnd = -1;
        int _rightEnd = -1;
        bool _empty = true;

        void Awake()
        {
            LeftEndButton?.onClick.AddListener(() => ChooseEnd(BoardEnd.Left));
            RightEndButton?.onClick.AddListener(() => ChooseEnd(BoardEnd.Right));
            HideEndButtons();
        }

        void ChooseEnd(BoardEnd end)
        {
            HideEndButtons();
            OnEndChosen?.Invoke(end);
        }

        /// <summary>Adds a played tile to the visual chain.</summary>
        public void AddTile(int tileIndex, BoardEnd end, int seat)
        {
            HideEndButtons();
            if (LayoutView == null) return;

            var tile = DominoSet.CreateFullSet()[tileIndex];

            if (_empty)
            {
                // Mirrors GameBoard.PlaceFirstTile: LeftEnd = SideA, RightEnd = SideB.
                LayoutView.PlaceFirst(new PlacedTile(tile, tile.SideA, tile.SideB));
                _leftEnd  = tile.SideA;
                _rightEnd = tile.SideB;
                _empty = false;
                return;
            }

            if (end == BoardEnd.Left)
            {
                int outward = tile.Other(_leftEnd);
                LayoutView.Append(new PlacedTile(tile, outward, _leftEnd), BoardEnd.Left);
                _leftEnd = outward;
            }
            else
            {
                int outward = tile.Other(_rightEnd);
                LayoutView.Append(new PlacedTile(tile, _rightEnd, outward), BoardEnd.Right);
                _rightEnd = outward;
            }
        }

        /// <summary>
        /// Shows the end-choice buttons for a tile that can play on either end.
        /// Button labels display the pip value they connect to.
        /// </summary>
        public void HighlightValidEnds(DominoTile tile)
        {
            if (_empty) return;

            if (LeftEndButton != null && tile.Has(_leftEnd))
            {
                LeftEndButton.gameObject.SetActive(true);
                var label = LeftEndButton.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = $"◀ {_leftEnd}";
            }
            if (RightEndButton != null && tile.Has(_rightEnd))
            {
                RightEndButton.gameObject.SetActive(true);
                var label = RightEndButton.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = $"{_rightEnd} ▶";
            }
        }

        public void HideEndButtons()
        {
            if (LeftEndButton  != null) LeftEndButton.gameObject.SetActive(false);
            if (RightEndButton != null) RightEndButton.gameObject.SetActive(false);
        }

        /// <summary>Clears the chain at the start of a new round.</summary>
        public void Clear()
        {
            LayoutView?.Clear();
            _leftEnd = _rightEnd = -1;
            _empty = true;
            HideEndButtons();
        }
    }
}
