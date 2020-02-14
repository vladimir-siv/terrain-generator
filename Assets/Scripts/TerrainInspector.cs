﻿using System;
using System.Linq;
using System.Reflection;
using System.IO;
using UnityEngine;
using UnityEditor;
using Terrain = TerrainGenerator.Terrain;

public class TerrainInspector : EditorWindow
{
	[MenuItem("Window/Terrain/Inspector")]
	public static void Open() { GetWindow<TerrainInspector>("Terrain Inspector"); }

	private static TerrainCreatorController CurrentController => GameObject.FindGameObjectWithTag("Terrain")?.GetComponent<TerrainCreatorController>();

	private TerrainCreatorController Controller = null;

	private FieldInfo PropTerrainGranularity = null;
	private FieldInfo PropTerrainStep = null;
	private FieldInfo PropTerrainScale = null;
	private FieldInfo PropBrushRadius = null;
	private FieldInfo PropBrushZCorretion = null;
	private FieldInfo PropBrushDelta = null;
	private PropertyInfo PropObservedTerrain = null;
	private PropertyInfo PropTerrainMesh = null;

	private string TerrainGranularity => PropTerrainGranularity?.GetValue(Controller)?.ToString() ?? "[Try refreshing]";
	private string TerrainStep => PropTerrainStep?.GetValue(Controller)?.ToString() ?? "[Try refreshing]";
	private string TerrainScale => PropTerrainScale?.GetValue(Controller)?.ToString() ?? "[Try refreshing]";
	private string BrushRadius => PropBrushRadius?.GetValue(Controller)?.ToString() ?? "[Try refreshing]";
	private string BrushZCorretion => PropBrushZCorretion?.GetValue(Controller)?.ToString() ?? "[Try refreshing]";
	private float BrushDelta { get => (float)PropBrushDelta.GetValue(Controller); set => PropBrushDelta.SetValue(Controller, value); }
	private Terrain ObservedTerrain => (Terrain)PropObservedTerrain?.GetValue(Controller);
	private Mesh TerrainMesh => (Mesh)PropTerrainMesh?.GetValue(Controller);

	private bool NewTerrainRandom = false;
	private int NewTerrainGranularity = 50;
	private float NewTerrainStep = 0.1f;
	private float NewTerrainScale = 10.0f;
	private Vector3 VirtualBrushPosition = new Vector3(0f, 0f, 0f);
	private float VirtualBrushRadius = 0.1f;
	private float VirtualBrushDelta = +1.0f;
	private byte CubeState = 0;
	private bool RenderCubeState = false;
	private byte? RenderedCubeState = null;
	private bool VerboseRenderCube = false;

	private Vector3[] Corners = null;
	private bool[] Negatives = null;
	private GameObject[] IndicationSpheres = null;

	private void Refresh()
	{
		Controller = CurrentController;
		if (Controller == null) return;
		var ctype = Controller.GetType();

		PropTerrainGranularity = ctype.GetField("TerrainGranularity", BindingFlags.Instance | BindingFlags.NonPublic);
		PropTerrainStep = ctype.GetField("TerrainStep", BindingFlags.Instance | BindingFlags.NonPublic);
		PropTerrainScale = ctype.GetField("TerrainScale", BindingFlags.Instance | BindingFlags.NonPublic);
		PropBrushRadius = ctype.GetField("BrushRadius", BindingFlags.Instance | BindingFlags.NonPublic);
		PropBrushZCorretion = ctype.GetField("BrushZCorretion", BindingFlags.Instance | BindingFlags.NonPublic);
		PropBrushDelta = ctype.GetField("BrushDelta", BindingFlags.Instance | BindingFlags.NonPublic);
		PropObservedTerrain = ctype.GetProperty("ObservedTerrain", BindingFlags.Instance | BindingFlags.NonPublic);
		PropTerrainMesh = ctype.GetProperty("TerrainMesh", BindingFlags.Instance | BindingFlags.NonPublic);

		RenderedCubeState = null;
	}

	private void OnGUI()
	{
		// Base options
		if (Controller == null) Refresh();
		GUILayout.Space(10f);
		GUILayout.Label("Base options", EditorStyles.boldLabel);
		if (GUILayout.Button("Refresh")) Refresh();
		if (Controller == null) return;

		// Terrain properties
		GUILayout.Space(10f);
		GUILayout.Label("Terrain Properties", EditorStyles.boldLabel);
		GUILayout.Label($"Terrain Granularity:\t{TerrainGranularity}");
		GUILayout.Label($"Terrain Step:\t\t{TerrainStep}");
		GUILayout.Label($"Terrain Scale:\t\t{TerrainScale}");
		GUILayout.Label($"Brush Radius:\t\t{BrushRadius}");
		GUILayout.Label($"Brush Z Correction:\t{BrushZCorretion}");
		if (PropBrushDelta != null) BrushDelta = EditorGUILayout.Slider("Brush Delta:", BrushDelta, 0.01f, 1.0f);
		else GUILayout.Label("Brush Delta:\t\t[Try refreshing]");

		// Terrain generation
		GUILayout.Space(10f);
		GUILayout.Label("Terrain generation", EditorStyles.boldLabel);
		NewTerrainRandom = EditorGUILayout.ToggleLeft("Generate Random Terrain", NewTerrainRandom);
		NewTerrainGranularity = EditorGUILayout.IntField("Terrain Granularity:", NewTerrainGranularity);
		NewTerrainStep = EditorGUILayout.FloatField("Terrain Step:", NewTerrainStep);
		NewTerrainScale = EditorGUILayout.FloatField("Terrain Scale:", NewTerrainScale);
		GUI.enabled = Application.isPlaying;
		if (GUILayout.Button("Generate")) GenerateTerrain(demanded: true, !NewTerrainRandom, NewTerrainGranularity, NewTerrainStep, NewTerrainScale);
		GUI.enabled = true;

		// Brush actions
		GUILayout.Space(10f);
		GUILayout.Label("Brush Actions", EditorStyles.boldLabel);
		VirtualBrushPosition = EditorGUILayout.Vector3Field("Virtual Brush Position:", VirtualBrushPosition);
		VirtualBrushRadius = EditorGUILayout.FloatField("Virtual Brush Radius:", VirtualBrushRadius);
		VirtualBrushDelta = EditorGUILayout.FloatField("Virtual Brush Delta:", VirtualBrushDelta);
		GUI.enabled = Application.isPlaying;
		if (GUILayout.Button("Apply")) ApplyBrush(VirtualBrushPosition, VirtualBrushRadius, VirtualBrushDelta);
		GUI.enabled = true;

		// Terrain debugging
		GUILayout.Space(10f);
		GUILayout.Label("Terrain Debugging", EditorStyles.boldLabel);
		GUILayout.BeginHorizontal();
		if (GUILayout.Button("Verbose Simple Terrain")) VerboseSimpleTerrain();
		GUI.enabled = Application.isPlaying;
		if (GUILayout.Button("Verbose Current Terrain")) VerboseCurrentTerrain();
		GUI.enabled = true;
		GUILayout.EndHorizontal();
		GUILayout.BeginHorizontal();
		if (GUILayout.Button("<")) --CubeState;
		GUI.skin.GetStyle("Label").alignment = TextAnchor.MiddleCenter;
		GUILayout.Label($"Cube State: {CubeState:000}");
		GUI.skin.GetStyle("Label").alignment = TextAnchor.MiddleLeft;
		if (GUILayout.Button(">")) ++CubeState;
		GUILayout.EndHorizontal();
		GUILayout.BeginHorizontal();
		GUI.enabled = Application.isPlaying;
		if (!Application.isPlaying) RenderCubeState = false;
		RenderCubeState = EditorGUILayout.ToggleLeft("Render Cube State", RenderCubeState, GUILayout.MinWidth(120f));
		VerboseRenderCube = EditorGUILayout.ToggleLeft("Verbose Render", VerboseRenderCube, GUILayout.MinWidth(120f));
		GUI.enabled = true;
		GUILayout.EndHorizontal();
		if (RenderCubeState) RenderCube();
		else RenderedCubeState = null;
	}

	private void Update()
	{
		if (EditorApplication.isPlaying && !EditorApplication.isPaused)
		{
			if (RenderedCubeState == null && IndicationSpheres != null)
			{
				for (var i = 0; i < IndicationSpheres.Length; ++i)
				{
					if (IndicationSpheres[i] != null && IndicationSpheres[i].activeSelf)
					{
						IndicationSpheres[i].SetActive(false);
					}
				}
			}

			Repaint();
		}
	}

	private void GenerateTerrain(bool demanded, bool empty = true, int terrainGranularity = 0, float terrainStep = 0.0f, float terrainScale = 0.0f)
	{
		if (demanded) RenderCubeState = false;
		var generator = Controller.GetType().GetMethod("GenerateTerrain", BindingFlags.Instance | BindingFlags.NonPublic);
		generator.Invoke(Controller, new object[] { empty, terrainGranularity, terrainStep, terrainScale });
	}

	private void ApplyBrush(Vector3 brushPosition, float brushRadius, float brushDelta)
	{
		brushPosition = new Vector3(brushPosition.z, brushPosition.y, brushPosition.x);
		ObservedTerrain.Update(brushPosition, brushRadius, brushDelta);
		ObservedTerrain.Calculate();
		ObservedTerrain.Triangulate();
		ObservedTerrain.GetMeshData(out var vertices, out var indices, out var normals);
		TerrainMesh.Clear();
		TerrainMesh.vertices = vertices;
		TerrainMesh.normals = normals;
		TerrainMesh.triangles = indices;
	}

	private void VerboseSimpleTerrain()
	{
		var granularity = 2;
		var step = 0.5f;
		var scale = 1.0f;
		var x = 1; var y = 1; var z = 1;
		var radius = 1.0f;
		var delta = +5.0f;

		using (var terrain = new Terrain())
		{
			terrain.GenerateEmpty(step, scale);
			terrain.Gridify(granularity);
			terrain.Update(new Vector3(terrain.Step * x, terrain.Step * y, terrain.Step * z), radius, delta);
			terrain.Calculate();

			var values_size = terrain.Size; // includes +1 already
			var target_size = terrain.Granularity + 1;
			values_size = values_size * values_size * values_size;
			target_size = target_size * target_size * target_size;

			var values = new float[values_size];
			var targets = new Vector3[target_size];
			var target_values = new float[target_size];

			terrain.GetValues(values);
			terrain.GetTargets(targets);
			terrain.GetTargetValues(target_values);

			var logsfolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "logs");
			if (!Directory.Exists(logsfolder)) Directory.CreateDirectory(logsfolder);
			
			File.WriteAllLines(Path.Combine(logsfolder, "values.txt"), values.Select(v => $"{v:F2}"));
			File.WriteAllLines(Path.Combine(logsfolder, "targets.txt"), targets.Select(v => $"{v}"));
			File.WriteAllLines(Path.Combine(logsfolder, "target_values.txt"), target_values.Select(v => $"{v:F2}"));
		}

		Debug.Log("Logs generated on Desktop.");
	}

	private void VerboseCurrentTerrain()
	{
		var terrain = ObservedTerrain;

		var values_size = terrain.Size; // includes +1 already
		var target_size = terrain.Granularity + 1;
		values_size = values_size * values_size * values_size;
		target_size = target_size * target_size * target_size;

		var values = new float[values_size];
		var targets = new Vector3[target_size];
		var target_values = new float[target_size];

		terrain.GetValues(values);
		terrain.GetTargets(targets);
		terrain.GetTargetValues(target_values);

		var logsfolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "logs");
		if (!Directory.Exists(logsfolder)) Directory.CreateDirectory(logsfolder);

		File.WriteAllLines(Path.Combine(logsfolder, "values.txt"), values.Select(v => $"{v:F2}"));
		File.WriteAllLines(Path.Combine(logsfolder, "targets.txt"), targets.Select(v => $"{v}"));
		File.WriteAllLines(Path.Combine(logsfolder, "target_values.txt"), target_values.Select(v => $"{v:F2}"));

		Debug.Log("Logs generated on Desktop.");
	}

	private void RenderCube()
	{
		GameObject CreateSphere()
		{
			var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			Destroy(sphere.GetComponent<SphereCollider>());
			sphere.GetComponent<MeshRenderer>().material.shader = Shader.Find("Unlit/ColorBlend");
			return sphere;
		}

		var terrain = ObservedTerrain;
		var mesh = TerrainMesh;

		if (terrain == null || mesh == null)
		{
			Debug.LogError("Failed to render cube. Try refreshing.");
			RenderCubeState = false;
			return;
		}

		if (RenderedCubeState == CubeState) return;
		RenderedCubeState = CubeState;

		var scale = terrain.Scale;
		if (terrain.Step != scale) GenerateTerrain(demanded: false, true, 1, scale, scale);
		if (terrain.Granularity != 1) terrain.Gridify(1);

		if (Corners == null || Corners.Length == 0) Corners = new Vector3[8];
		if (Negatives == null || Negatives.Length == 0) Negatives = new bool[8];
		if (IndicationSpheres == null || IndicationSpheres.Length == 0) IndicationSpheres = new GameObject[8];
		
		Corners[0] = new Vector3(scale * 0f, scale * 0f, scale * 0f);
		Corners[1] = new Vector3(scale * 1f, scale * 0f, scale * 0f);
		Corners[2] = new Vector3(scale * 1f, scale * 0f, scale * 1f);
		Corners[3] = new Vector3(scale * 0f, scale * 0f, scale * 1f);
		Corners[4] = new Vector3(scale * 0f, scale * 1f, scale * 0f);
		Corners[5] = new Vector3(scale * 1f, scale * 1f, scale * 0f);
		Corners[6] = new Vector3(scale * 1f, scale * 1f, scale * 1f);
		Corners[7] = new Vector3(scale * 0f, scale * 1f, scale * 1f);

		terrain.Clear();

		for (var i = 0; i < Corners.Length; ++i)
		{
			var sphere = IndicationSpheres[i];
			if (sphere == null) IndicationSpheres[i] = sphere = CreateSphere();
			Negatives[i] = (CubeState & (1 << i)) != 0;
			terrain.Update(Corners[i], 0.1f, +2f * (1 - Convert.ToInt32(Negatives[i])));
			sphere.transform.position = new Vector3(Corners[i].z, Corners[i].y, Corners[i].x);
			sphere.transform.localScale = new Vector3(scale / 10f, scale / 10f, scale / 10f);
			var material = sphere.GetComponent<MeshRenderer>().material;
			var color = Negatives[i] ? new Color(1.0f, 0.0f, 0.0f, 0.3f) : new Color(0.0f, 0.0f, 1.0f, 0.3f);
			material.SetColor("_Color", color);
			sphere.SetActive(true);
		}

		terrain.Calculate();
		terrain.Triangulate();
		terrain.GetMeshData(out var vertices, out var indices, out var normals);

		mesh.Clear();
		mesh.vertices = vertices;
		mesh.normals = normals;
		mesh.triangles = indices;

		if (VerboseRenderCube)
		{
			Debug.Log(string.Concat(vertices.Select(v => $"{v} ")));
			Debug.Log(string.Concat(indices.Select(v => $"{v} ")));
			Debug.Log(string.Concat(normals.Select(v => $"{v} ")));
		}
	}
}
