using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace TerrainGenerator.Inspector
{
	public sealed class IndicationCube
	{
		private static Font Arial = Resources.GetBuiltinResource<Font>("Arial.ttf");

		private List<Text> Overlays = new List<Text>(6);
		private Material CubeMaterial = null;

		public GameObject Reference { get; private set; }
		public float Resolution { get; private set; }

		private float _Value = 0.0f;
		private string ValueString => string.Format(CultureInfo.InvariantCulture, "{0:F2}", Value);
		public float Value
		{
			get { return _Value; }
			set { if (_Value == value) return; _Value = value; RefreshOverlay(); }
		}

		private Color _NegativeColor = new Color(1.0f, 0.0f, 0.0f, 0.3f);
		public Color NegativeColor
		{
			get { return _NegativeColor; }
			set { if (_NegativeColor == value) return; _NegativeColor = value; RefreshOverlay(); }
		}

		private Color _PositiveColor = new Color(0.0f, 0.0f, 1.0f, 0.3f);
		public Color PositiveColor
		{
			get { return _PositiveColor; }
			set { if (_PositiveColor == value) return; _PositiveColor = value; RefreshOverlay(); }
		}
		
		private (GameObject, Text) CreateTextOverlay()
		{
			(GameObject, Text) CreateText()
			{
				var textGO = new GameObject("Text", typeof(CanvasRenderer), typeof(Text));
				var textDisplay = textGO.GetComponent<Text>();
				var textRect = textDisplay.GetComponent<RectTransform>();
				textRect.position = Vector3.zero;
				textRect.sizeDelta = new Vector2(Resolution, Resolution);
				textGO.transform.position = Vector3.zero;
				textGO.transform.rotation = Quaternion.identity;
				textGO.transform.localScale = Vector3.one;
				textDisplay.font = Arial;
				textDisplay.alignment = TextAnchor.MiddleCenter;
				textDisplay.color = Color.white;
				textDisplay.fontSize = 60;
				return (textGO, textDisplay);
			}

			var canvasGO = new GameObject("Canvas", typeof(Canvas));
			var canvas = canvasGO.GetComponent<Canvas>();
			var canvasRect = canvas.GetComponent<RectTransform>();
			var scale = 1 / Resolution;
			canvas.renderMode = RenderMode.WorldSpace;
			canvasRect.position = Vector3.zero;
			canvasRect.sizeDelta = new Vector2(Resolution, Resolution);
			canvasGO.transform.position = Vector3.zero;
			canvasGO.transform.rotation = Quaternion.identity;
			canvasGO.transform.localScale = new Vector3(scale, scale, scale);

			var text = CreateText();
			text.Item1.transform.SetParent(canvasGO.transform, false);

			return (canvasGO, text.Item2);
		}

		private void GenerateOverlay(Quaternion rotation, Vector3 translation)
		{
			var text = CreateTextOverlay();
			text.Item2.transform.rotation = rotation;
			text.Item1.transform.position = translation;
			text.Item1.transform.SetParent(Reference.transform, false);
			Overlays.Add(text.Item2);
		}

		public IndicationCube(float resolution = 200f)
		{
			Reference = GameObject.CreatePrimitive(PrimitiveType.Cube);
			GameObject.Destroy(Reference.GetComponent<BoxCollider>());
			CubeMaterial = Reference.GetComponent<MeshRenderer>().material;
			CubeMaterial.shader = Shader.Find("Unlit/ColorBlend");
			CubeMaterial.SetColor("_Color", Color.black);
			Resolution = resolution;

			GenerateOverlay(Quaternion.Euler(+0f,	+0f,	+0f), new Vector3(+0f,		+0f,	-0.52f));
			GenerateOverlay(Quaternion.Euler(+0f,	+180f,	+0f), new Vector3(+0f,		+0f,	+0.52f));
			GenerateOverlay(Quaternion.Euler(+0f,	+90f,	+0f), new Vector3(-0.52f,	+0f,	+0f));
			GenerateOverlay(Quaternion.Euler(+0f,	-90f,	+0f), new Vector3(+0.52f,	+0f,	+0f));
			GenerateOverlay(Quaternion.Euler(+90f,	+0f,	+0f), new Vector3(+0f,		+0.52f,	+0f));
			GenerateOverlay(Quaternion.Euler(-90f,	+0f,	+0f), new Vector3(+0f,		-0.52f,	+0f));

			RefreshOverlay();
		}

		public void RefreshOverlay()
		{
			var value = ValueString;

			for (var i = 0; i < Overlays.Count; ++i)
			{
				Overlays[i].text = value;
			}

			if (Value <= 0) CubeMaterial.SetColor("_Color", NegativeColor);
			else CubeMaterial.SetColor("_Color", PositiveColor);
		}
	}
}
