using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Kapicua.AI;
using Kapicua.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Random = System.Random;

namespace Kapicua.Game
{
    /// <summary>
    /// Orchestrates the match: runs the RoundEngine, paces AI turns, routes human
    /// taps to moves, and keeps all the views and HUD in sync.
    /// Seating: player 0 = human (bottom), 1 = right rival, 2 = partner (top), 3 = left rival.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public BoardLayoutView Board;
        public HandView HumanHand;
        public OpponentHandView RightHand;
        public OpponentHandView TopHand;
        public OpponentHandView LeftHand;
        public GameHud Hud;
        public float AiDelay = 0.9f;

        static readonly string[] PlayerNames = { "Tú", "Rival derecha", "Tu compañero", "Rival izquierda" };

        readonly Random _rng = new Random();
        MatchState _match;
        RoundEngine _round;
        int _roundStarter;
        bool _awaitingHuman;
        Move? _pendingMove;
        DominoTile? _selectedTile;
        Camera _camera;

        void Start()
        {
            _camera = Camera.main;
            StartCoroutine(RunMatch());
        }

        IEnumerator RunMatch()
        {
            _match = new MatchState();
            Hud.SetScores(0, 0);

            while (!_match.IsOver)
                yield return RunRound();

            bool won = _match.WinningTeam == 0;
            bool restart = false;
            Hud.SetStatus("");
            Hud.ShowBanner(
                won ? "¡GANAMOS LA PARTIDA!" : "Perdimos la partida…",
                "Nueva partida",
                () => restart = true);
            while (!restart) yield return null;
            StartCoroutine(RunMatch());
        }

        IEnumerator RunRound()
        {
            _round = new RoundEngine(_rng, _match.IsFirstRound, _match.NextStartingPlayer);
            _roundStarter = _round.CurrentPlayer;
            _selectedTile = null;
            Board.Clear();
            RefreshViews();
            Hud.Toast(_match.IsFirstRound
                ? $"Abre {PlayerNames[_roundStarter]} con el doble seis"
                : $"Abre {PlayerNames[_roundStarter]}");

            while (!_round.IsOver)
            {
                if (_round.CurrentPlayer == 0) yield return HumanTurn();
                else yield return AITurn(_round.CurrentPlayer);
                RefreshViews();
            }

            var r = _round.Result;
            _match.ApplyRound(r, _roundStarter);
            Hud.SetScores(_match.TeamScores[0], _match.TeamScores[1]);

            bool done = false;
            Hud.ShowBanner(RoundSummary(r), _match.IsOver ? "Seguir" : "Siguiente ronda", () => done = true);
            while (!done) yield return null;
        }

        static string RoundSummary(RoundResult r)
        {
            if (r.WinningTeam < 0)
                return "Tranca empatada\nNadie anota";

            string who = r.WinningTeam == 0 ? "¡Ronda para NOSOTROS!" : "Ronda para ellos";
            string how = r.Blocked ? "Tranca" : (r.Capicua ? "¡¡CAPICÚA!! +30" : $"Dominó de {PlayerNames[r.WinningPlayer]}");
            return $"{who}\n{how}: +{r.Points} puntos";
        }

        IEnumerator HumanTurn()
        {
            _pendingMove = null;
            _selectedTile = null;

            if (_round.MustPass(0))
            {
                Hud.SetStatus("No llevas ficha — te toca pasar");
                RefreshViews();
                bool passed = false;
                Hud.ShowPass(() => passed = true);
                while (!passed) yield return null;
                _round.Pass(0);
                Hud.Toast("Pasaste");
                yield break;
            }

            Hud.SetStatus("Te toca — elige una ficha");
            _awaitingHuman = true;
            RefreshViews();
            while (_pendingMove == null) yield return null;
            _awaitingHuman = false;
            Hud.HideEndChoice();
            PlayMove(0, _pendingMove.Value);
        }

        IEnumerator AITurn(int player)
        {
            Hud.SetStatus($"Juega {PlayerNames[player]}…");
            yield return new WaitForSeconds(AiDelay);

            if (_round.MustPass(player))
            {
                _round.Pass(player);
                Hud.Toast($"{PlayerNames[player]} pasa");
            }
            else
            {
                PlayMove(player, DominoAI.ChooseMove(_round, player));
            }
        }

        void PlayMove(int player, Move move)
        {
            bool wasEmpty = _round.Board.IsEmpty;
            DominoTile prevFirst = wasEmpty ? default : _round.Board.Tiles[0].Tile;

            _round.Play(player, move);

            var tiles = _round.Board.Tiles;
            if (wasEmpty) Board.PlaceFirst(tiles[0]);
            else if (tiles[0].Tile != prevFirst) Board.Append(tiles[0], BoardEnd.Left);
            else Board.Append(tiles[tiles.Count - 1], BoardEnd.Right);
        }

        void RefreshViews()
        {
            var playable = new HashSet<DominoTile>(_round.GetLegalMoves(0).Select(m => m.Tile));
            HumanHand.Refresh(_round.Hands[0], playable, _selectedTile, _awaitingHuman);
            RightHand.Refresh(_round.Hands[1].Count);
            TopHand.Refresh(_round.Hands[2].Count);
            LeftHand.Refresh(_round.Hands[3].Count);
        }

        void Update()
        {
            if (!_awaitingHuman || _pendingMove.HasValue) return;

            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Vector2 screenPos = pointer.position.ReadValue();
            var ray = _camera.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out var hit, 200f)) return;

            var view = hit.collider.GetComponentInParent<TileView>();
            if (view != null && HumanHand.Owns(view))
                OnHandTileTapped(view.Tile);
        }

        void OnHandTileTapped(DominoTile tile)
        {
            var legal = _round.GetLegalMoves(0).Where(m => m.Tile == tile).ToList();
            if (legal.Count == 0)
            {
                Hud.Toast("Esa ficha no encaja");
                return;
            }

            _selectedTile = tile;
            RefreshViews();

            if (legal.Count == 1)
            {
                Hud.HideEndChoice();
                _pendingMove = legal[0];
            }
            else
            {
                Hud.ShowEndChoice(
                    () => _pendingMove = legal.First(m => m.End == BoardEnd.Left),
                    () => _pendingMove = legal.First(m => m.End == BoardEnd.Right));
            }
        }
    }
}
