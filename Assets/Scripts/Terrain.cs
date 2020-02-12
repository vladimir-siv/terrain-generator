using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TerrainGenerator
{
	public sealed class Terrain : IDisposable
	{
		private static readonly ComputeShader TrilinearInterpolator = Resources.Load<ComputeShader>("TrilinearInterpolation");
		private static readonly ComputeShader TerrainAdjustor = Resources.Load<ComputeShader>("TerrainAdjustment");
		private static readonly ComputeShader Gridificator = Resources.Load<ComputeShader>("Gridification");
		private static readonly ComputeShader Triangulator = Resources.Load<ComputeShader>("Triangulation");
		private static readonly uint[] Counters = new[] { 0u };

		private readonly object Sync = new object();

		private ComputeBuffer Values = null;
		private ComputeBuffer Targets = null;
		private ComputeBuffer TargetValues = null;

		private ComputeBuffer Vertices = null;
		private ComputeBuffer Indices = null;
		private ComputeBuffer Normals = null;

		private ComputeBuffer Counter = new ComputeBuffer(1, sizeof(uint));
		private uint[] CounterCache = new uint[1];
		
		public float Step { get; private set; } = 0.1f;
		public float Scale { get; private set; } = 10.0f;
		public float Min { get; private set; } = -20.0f;
		public float Max { get; private set; } = +20.0f;
		public int Size => (int)Math.Ceiling(Scale / Step);

		public void GenerateRandom() => GenerateRandom(Step, Scale, Min, Max);
		public void GenerateRandom(float step) => GenerateRandom(step, Scale, Min, Max);
		public void GenerateRandom(float step, float scale) => GenerateRandom(step, scale, Min, Max);
		public void GenerateRandom(float step, float scale, float range) => GenerateRandom(step, scale, -range / 2.0f, +range / 2.0f);
		public void GenerateRandom(float step, float scale, float min, float max) => Generate(step, scale, min, max, null);

		public void GenerateFlat() => GenerateFlat(Step, Scale, Min, Max, Min);
		public void GenerateFlat(float step) => GenerateFlat(step, Scale, Min, Max, Min);
		public void GenerateFlat(float step, float scale) => GenerateFlat(step, scale, Min, Max, Min);
		public void GenerateFlat(float step, float scale, float range) => GenerateFlat(step, scale, -range / 2.0f, +range / 2.0f, -range / 2.0f);
		public void GenerateFlat(float step, float scale, float range, float initial) => GenerateFlat(step, scale, -range / 2.0f, +range / 2.0f, initial);
		public void GenerateFlat(float step, float scale, float min, float max, float initial) => Generate(step, scale, min, max, initial);

		public void Generate(float step, float scale, float min, float max, float? initial)
		{
			lock (Sync)
			{
				if (step <= 0.0f) throw new ArgumentException(nameof(step));
				if (scale <= 0.0f) throw new ArgumentException(nameof(scale));
				if (min >= 0.0f) throw new ArgumentException(nameof(step));
				if (max <= 0.0f) throw new ArgumentException(nameof(step));
				if (initial != null && initial < min || max < initial) throw new ArgumentException(nameof(step));

				Step = step;
				Scale = scale;
				Min = min;
				Max = max;

				var size1 = Size;
				var size2 = size1 * size1;
				var size3 = size2 * size1;

				var initial_values = new List<float>(size3);

				// Optimize this & create smart random
				for (var i = 0; i < size1; ++i)
					for (var j = 0; j < size1; ++j)
						for (var k = 0; k < size1; ++k)
							initial_values.Add(initial ?? Random.Range(min, max));

				Values?.Release();
				Values = new ComputeBuffer(initial_values.Count, sizeof(float));
				Values.SetData(initial_values);

				Targets?.Release();
				Targets = null;
				TargetValues?.Release();
				TargetValues = null;
				Vertices?.Release();
				Vertices = null;
				Indices?.Release();
				Indices = null;
				Normals?.Release();
				Normals = null;
			}
		}

		public void UpdateTerrain(Vector3 center, float radius, float delta)
		{
			lock (Sync)
			{
				if (Values == null) throw new InvalidOperationException("Terrain not generated. Call Generate() before this method.");

				lock (TerrainAdjustor)
				{
					var main = TerrainAdjustor.FindKernel("main");

					TerrainAdjustor.SetBuffer(main, "_values", Values);
					TerrainAdjustor.SetFloat("_step", Step);
					TerrainAdjustor.SetFloat("_scale", Scale);
					TerrainAdjustor.SetFloat("_min", Min);
					TerrainAdjustor.SetFloat("_max", Max);

					TerrainAdjustor.SetVector("_center", center);
					TerrainAdjustor.SetFloat("_radius", radius);
					TerrainAdjustor.SetFloat("_delta", delta);

					var size = Size;
					TerrainAdjustor.Dispatch(main, size, size, size);
				}
			}
		}

		public void SetTargets(int granularity)
		{
			lock (Sync)
			{
				if (Values == null) throw new InvalidOperationException("Terrain not generated. Call Generate() before this method.");
				if (granularity <= 0) throw new ArgumentException("Granularity cannot be less than or equal to 0.");

				Targets?.Release();
				TargetValues?.Release();

				Vertices?.Release();
				Indices?.Release();
				Normals?.Release();

				var vertexCnt = granularity * granularity * granularity * 15;
				Vertices = new ComputeBuffer(vertexCnt, 3 * sizeof(float));
				Indices = new ComputeBuffer(vertexCnt, sizeof(int));
				Normals = new ComputeBuffer(vertexCnt, 3 * sizeof(float));

				++granularity;
				var targetCnt = granularity * granularity * granularity;

				Targets = new ComputeBuffer(targetCnt, 3 * sizeof(float));

				TargetValues = new ComputeBuffer(targetCnt, sizeof(float));

				lock (Gridificator)
				{
					var main = Gridificator.FindKernel("main");

					Gridificator.SetBuffer(main, "_targets", Targets);
					Gridificator.SetInt("_granularity", granularity);
					Gridificator.SetFloat("_scale", Scale);
					Gridificator.Dispatch(main, granularity, granularity, granularity);
				}
			}
		}

		public void Calculate()
		{
			lock (Sync)
			{
				if (Targets == null || TargetValues == null) throw new InvalidOperationException("Targets are not set. Call SetTargets() before this method.");
				
				lock (TrilinearInterpolator)
				{
					var dimension = (int)Math.Ceiling(Math.Pow(Targets.count, 1.0 / 3.0) + 1e-9);

					var main = TrilinearInterpolator.FindKernel("main");

					TrilinearInterpolator.SetBuffer(main, "_values", Values);
					TrilinearInterpolator.SetFloat("_step", Step);
					TrilinearInterpolator.SetFloat("_scale", Scale);
					TrilinearInterpolator.SetFloat("_min", Min);
					TrilinearInterpolator.SetFloat("_max", Max);
					TrilinearInterpolator.SetInt("_target_count", Targets.count);
					TrilinearInterpolator.SetBuffer(main, "_targets", Targets);
					TrilinearInterpolator.SetBuffer(main, "_target_values", TargetValues);
					TrilinearInterpolator.SetInt("_dimension", dimension);

					TrilinearInterpolator.Dispatch(main, dimension, dimension, dimension);
				}
			}
		}

		// [Unused]
		public void GetTargetValues(float[] targetValues)
		{
			lock (Sync)
			{
				if (targetValues == null) throw new ArgumentNullException(nameof(targetValues));
				if (targetValues.Length != TargetValues.count) throw new ArgumentException(nameof(targetValues));

				TargetValues.GetData(targetValues);
			}
		}

		public void Triangulate(int granularity)
		{
			lock (Sync)
			{
				if (Targets == null || TargetValues == null) throw new InvalidOperationException("Targets are not set. Call SetTargets() before this method.");
				if (granularity <= 0) throw new ArgumentException("Granularity cannot be less than or equal to 0.");

				var size = granularity + 1;
				size = size * size * size;

				if (size != Targets.count) throw new ArgumentException(nameof(granularity));

				lock (Triangulator)
				{
					Counter.SetData(Counters);

					var main = Triangulator.FindKernel("main");

					Triangulator.SetFloat("_step", Step);
					Triangulator.SetFloat("_scale", Scale);
					Triangulator.SetFloat("_min", Min);
					Triangulator.SetFloat("_max", Max);
					Triangulator.SetInt("_granularity", granularity);
					Triangulator.SetInt("_target_count", size);
					Triangulator.SetBuffer(main, "_targets", Targets);
					Triangulator.SetBuffer(main, "_target_values", TargetValues);
					Triangulator.SetBuffer(main, "_vertices", Vertices);
					Triangulator.SetBuffer(main, "_indices", Indices);
					Triangulator.SetBuffer(main, "_normals", Normals);
					Triangulator.SetBuffer(main, "_cnt", Counter);

					Triangulator.Dispatch(main, granularity, granularity, granularity);
				}
			}
		}

		public void GetTerrainMeshData(out Vector3[] vertices, out int[] indices, out Vector3[] normals)
		{
			lock (Sync)
			{
				if (Vertices == null || Indices == null || Normals == null) throw new InvalidOperationException("Cannot read terrain mesh data when none was generated. Call Triangulate() before this method.");

				Counter.GetData(CounterCache);
				var count = (int)CounterCache[0];

				vertices = new Vector3[count];
				indices = new int[count];
				normals = new Vector3[count];

				Vertices.GetData(vertices, 0, 0, count);
				Indices.GetData(indices, 0, 0, count);
				Normals.GetData(normals, 0, 0, count);
			}
		}

		public void Dispose()
		{
			lock (Sync)
			{
				Values?.Release();
				Targets?.Release();
				TargetValues?.Release();
				Vertices?.Release();
				Indices?.Release();
				Normals?.Release();
				Counter?.Release();

				Values = null;
				Targets = null;
				TargetValues = null;
				Vertices = null;
				Indices = null;
				Normals = null;
				Counter = null;
			}
		}
	}
}
