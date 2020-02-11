﻿using System;
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
		private ComputeBuffer Outputs = null;

		public float Min { get; private set; } = -20.0f;
		public float Max { get; private set; } = +20.0f;
		public float Step { get; private set; } = 0.1f;
		public int Size => (int)Math.Ceiling((Max - Min) / Step);

		public void GenerateRandom() => GenerateRandom(Min, Max, Step);
		public void GenerateRandom(float range) => GenerateRandom(range, Step);
		public void GenerateRandom(float range, float step) => GenerateRandom(-range / 2.0f, +range / 2.0f, step);
		public void GenerateRandom(float min, float max, float step) => Generate(min, max, step, null);

		public void GenerateFlat() => GenerateFlat(Min, Max, Step, Min);
		public void GenerateFlat(float range) => GenerateFlat(range, Step);
		public void GenerateFlat(float range, float step) => GenerateFlat(range, step, -range / 2.0f);
		public void GenerateFlat(float range, float step, float initial) => GenerateFlat(-range / 2.0f, +range / 2.0f, step, initial);
		public void GenerateFlat(float min, float max, float step, float initial) => Generate(min, max, step, initial);

		public void Generate(float min, float max, float step, float? initial)
		{
			lock (Sync)
			{
				if (min >= 0.0f) throw new ArgumentException(nameof(step));
				if (max <= 0.0f) throw new ArgumentException(nameof(step));
				if (step <= 0.0f) throw new ArgumentException(nameof(step));
				if (initial != null && initial < min || max < initial) throw new ArgumentException(nameof(step));

				Min = min;
				Max = max;
				Step = step;

				var size1 = (int)Math.Ceiling((Max - Min) / Step);
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
				Outputs?.Release();
				Outputs = null;
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
					TerrainAdjustor.SetFloat("_min", Min);
					TerrainAdjustor.SetFloat("_max", Max);
					TerrainAdjustor.SetFloat("_step", Step);

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
				Outputs?.Release();

				Targets = new ComputeBuffer(targets.Count, 3 * sizeof(float));
				Targets.SetData(targets);

				Outputs = new ComputeBuffer(targets.Count, sizeof(float));
			}
		}

		public void Calculate(float[] output)
		{
			lock (Sync)
			{
				if (Targets == null || Outputs == null) throw new InvalidOperationException("Targets are not set. Call SetTargets() before this method.");
				if (output == null) throw new ArgumentNullException(nameof(output));
				if (output.Length != Outputs.count) throw new ArgumentException(nameof(output));
				
				lock (TrilinearInterpolator)
				{
					var main = TrilinearInterpolator.FindKernel("main");

					TrilinearInterpolator.SetBuffer(main, "_values", Values);
					TrilinearInterpolator.SetFloat("_min", Min);
					TrilinearInterpolator.SetFloat("_max", Max);
					TrilinearInterpolator.SetFloat("_step", Step);
					TrilinearInterpolator.SetFloat("_target_count", Targets.count);
					TrilinearInterpolator.SetBuffer(main, "_targets", Targets);
					TrilinearInterpolator.SetBuffer(main, "_outputs", Outputs);

					TrilinearInterpolator.Dispatch(main, Targets.count, 1, 1);

					Outputs.GetData(output);
				}
			}
		}

		public void Dispose()
		{
			lock (Sync)
			{
				Values?.Release();
				Targets?.Release();
				Outputs?.Release();

				Values = null;
				Targets = null;
				Outputs = null;
			}
		}
	}
}