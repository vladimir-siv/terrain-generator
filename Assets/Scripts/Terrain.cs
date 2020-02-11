using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TerrainGenerator
{
	public sealed class Terrain : IDisposable
	{
		private static ComputeShader TrilinearInterpolator = Resources.Load<ComputeShader>("TrilinearInterpolation");
		private static ComputeShader TerrainAdjustor = Resources.Load<ComputeShader>("TerrainAdjustment");

		private object Sync = new object();

		private ComputeBuffer Values = null;
		private ComputeBuffer Targets = null;
		private ComputeBuffer TargetValues = null;

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

		public void SetTargets(List<Vector3> targets)
		{
			lock (Sync)
			{
				if (Values == null) throw new InvalidOperationException("Terrain not generated. Call Generate() before this method.");

				Targets?.Release();
				TargetValues?.Release();

				Targets = new ComputeBuffer(targets.Count, 3 * sizeof(float));
				Targets.SetData(targets);

				TargetValues = new ComputeBuffer(targets.Count, sizeof(float));
			}
		}

		public void Calculate()
		{
			lock (Sync)
			{
				if (Targets == null || TargetValues == null) throw new InvalidOperationException("Targets are not set. Call SetTargets() before this method.");
				
				lock (TrilinearInterpolator)
				{
					var main = TrilinearInterpolator.FindKernel("main");

					TrilinearInterpolator.SetBuffer(main, "_values", Values);
					TrilinearInterpolator.SetFloat("_step", Step);
					TrilinearInterpolator.SetFloat("_scale", Scale);
					TrilinearInterpolator.SetFloat("_min", Min);
					TrilinearInterpolator.SetFloat("_max", Max);
					TrilinearInterpolator.SetFloat("_target_count", Targets.count);
					TrilinearInterpolator.SetBuffer(main, "_targets", Targets);
					TrilinearInterpolator.SetBuffer(main, "_target_values", TargetValues);

					TrilinearInterpolator.Dispatch(main, Targets.count, 1, 1);
				}
			}
		}

		public void GetTargetValues(float[] targetValues)
		{
			lock (Sync)
			{
				if (targetValues == null) throw new ArgumentNullException(nameof(targetValues));
				if (targetValues.Length != TargetValues.count) throw new ArgumentException(nameof(targetValues));

				TargetValues.GetData(targetValues);
			}
		}

		public void Dispose()
		{
			lock (Sync)
			{
				Values?.Release();
				Targets?.Release();
				TargetValues?.Release();

				Values = null;
				Targets = null;
				TargetValues = null;
			}
		}
	}
}
