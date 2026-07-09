using Kapicua.Core;
using UnityEngine;

namespace Kapicua.Game
{
    /// <summary>
    /// Renders one domino tile from primitives at runtime: a body cube, a divider bar,
    /// and sphere pips laid out on a 3x3 grid per half. Local axes: X = width,
    /// Y = thickness, Z = length. Value halves sit at local -Z and +Z.
    /// </summary>
    public class TileView : MonoBehaviour
    {
        public const float Length = 2f;
        public const float Width = 1f;
        public const float Thickness = 0.3f;
        const float TopY = Thickness / 2f + 0.005f;
        const float PipGrid = 0.24f;

        public DominoTile Tile { get; private set; }

        static Material _bodyMat, _pipMat;
        MeshRenderer _bodyRenderer;

        // (x, z) grid offsets per pip value, in units of PipGrid.
        static readonly Vector2[][] PipPatterns =
        {
            new Vector2[0],
            new[] { new Vector2(0, 0) },
            new[] { new Vector2(-1, -1), new Vector2(1, 1) },
            new[] { new Vector2(-1, -1), new Vector2(0, 0), new Vector2(1, 1) },
            new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(1, 1) },
            new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(0, 0), new Vector2(1, -1), new Vector2(1, 1) },
            new[] { new Vector2(-1, -1), new Vector2(-1, 0), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(1, 0), new Vector2(1, 1) },
        };

        static Material BodyMat
        {
            get
            {
                if (_bodyMat == null)
                {
                    _bodyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    _bodyMat.color = new Color(0.96f, 0.94f, 0.87f);
                    _bodyMat.SetFloat("_Smoothness", 0.55f);
                }
                return _bodyMat;
            }
        }

        static Material PipMat
        {
            get
            {
                if (_pipMat == null)
                {
                    _pipMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    _pipMat.color = new Color(0.10f, 0.10f, 0.12f);
                    _pipMat.SetFloat("_Smoothness", 0.4f);
                }
                return _pipMat;
            }
        }

        /// <param name="negZValue">Which of the tile's values sits on the local -Z half
        /// (controls orientation on the board). Defaults to Tile.High.</param>
        public static TileView Create(DominoTile tile, bool faceUp, int negZValue = -1)
        {
            var root = new GameObject($"Tile {tile}");
            var view = root.AddComponent<TileView>();
            view.Build(tile, negZValue < 0 ? tile.High : negZValue, faceUp);
            return view;
        }

        void Build(DominoTile tile, int negZValue, bool faceUp)
        {
            Tile = tile;

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(transform, false);
            body.transform.localScale = new Vector3(Width, Thickness, Length);
            _bodyRenderer = body.GetComponent<MeshRenderer>();
            _bodyRenderer.sharedMaterial = BodyMat;

            var face = new GameObject("Face").transform;
            face.SetParent(transform, false);
            // Rotating the face container 180° about Z mirrors it under the tile.
            if (!faceUp) face.localRotation = Quaternion.Euler(0, 0, 180);

            AddBox(face, new Vector3(Width * 0.86f, 0.02f, 0.05f), new Vector3(0, TopY, 0));
            AddPips(face, negZValue, -Length / 4f);
            AddPips(face, tile.Other(negZValue), Length / 4f);
        }

        void AddBox(Transform parent, Vector3 scale, Vector3 pos)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Divider";
            Destroy(box.GetComponent<Collider>());
            box.transform.SetParent(parent, false);
            box.transform.localScale = scale;
            box.transform.localPosition = pos;
            box.GetComponent<MeshRenderer>().sharedMaterial = PipMat;
        }

        void AddPips(Transform parent, int value, float halfCenterZ)
        {
            foreach (var p in PipPatterns[value])
            {
                var pip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pip.name = $"Pip{value}";
                Destroy(pip.GetComponent<Collider>());
                pip.transform.SetParent(parent, false);
                pip.transform.localScale = new Vector3(0.17f, 0.06f, 0.17f);
                pip.transform.localPosition = new Vector3(p.x * PipGrid, TopY, halfCenterZ + p.y * PipGrid);
                pip.GetComponent<MeshRenderer>().sharedMaterial = PipMat;
            }
        }

        public void SetTint(Color color)
        {
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_BaseColor", color);
            _bodyRenderer.SetPropertyBlock(mpb);
        }
    }
}
