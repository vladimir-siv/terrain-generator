using System;
using UnityEngine;

namespace TerrainGenerator
{
	public sealed class Terrain : IDisposable
	{
		private static readonly ComputeShader TerrainInitializator = Resources.Load<ComputeShader>("TerrainInitialization");
		private static readonly ComputeShader TerrainAdjustor = Resources.Load<ComputeShader>("TerrainAdjustment");
		private static readonly ComputeShader Gridificator = Resources.Load<ComputeShader>("Gridification");
		private static readonly ComputeShader TrilinearInterpolator = Resources.Load<ComputeShader>("TrilinearInterpolation");
		private static readonly ComputeShader Triangulator = Resources.Load<ComputeShader>("Triangulation");
		private static readonly uint[] ResetCounters = new[] { 0u };

		private readonly object Sync = new object();

		private ComputeBuffer Values = null;
		private ComputeBuffer Targets = null;
		private ComputeBuffer TargetValues = null;

		private ComputeBuffer Vertices = null;
		private ComputeBuffer Indices = null;
		private ComputeBuffer Normals = null;

		private ComputeBuffer Counters = null;
		private uint[] CountersCache = new uint[1];
		
		public float Step { get; private set; } = 0.1f;
		public float Scale { get; private set; } = 10.0f;
		public float Min { get; private set; } = -20.0f;
		public float Max { get; private set; } = +20.0f;
		public int Granularity { get; private set; } = 0;
		public int Size => (int)Math.Ceiling(Scale / Step) + 1;

		public void GenerateRandom(int layers) => GenerateRandom(Step, Scale, Min, Max, layers);
		public void GenerateRandom(float step, float scale, int layers) => GenerateRandom(step, scale, Min, Max, layers);
		public void GenerateRandom() => GenerateRandom(Step, Scale, Min, Max, 4);
		public void GenerateRandom(float step) => GenerateRandom(step, Scale, Min, Max, 4);
		public void GenerateRandom(float step, float scale) => GenerateRandom(step, scale, Min, Max, 4);
		public void GenerateRandom(float step, float scale, float range) => GenerateRandom(step, scale, -range / 2.0f, +range / 2.0f, 4);
		public void GenerateRandom(float step, float scale, float range, int layers) => GenerateRandom(step, scale, -range / 2.0f, +range / 2.0f, layers);
		public void GenerateRandom(float step, float scale, float min, float max, int layers) => Generate(step, scale, min, max, layers, true);

		public void GenerateEmpty() => GenerateEmpty(Step, Scale, Min, Max, -1.0f);
		public void GenerateEmpty(float step) => GenerateEmpty(step, Scale, Min, Max, -1.0f);
		public void GenerateEmpty(float step, float scale) => GenerateEmpty(step, scale, Min, Max, -1.0f);
		public void GenerateEmpty(float step, float scale, float range) => GenerateEmpty(step, scale, -range / 2.0f, +range / 2.0f, -1.0f);
		public void GenerateEmpty(float step, float scale, float range, float initial) => GenerateEmpty(step, scale, -range / 2.0f, +range / 2.0f, initial);
		public void GenerateEmpty(float step, float scale, float min, float max, float initial) => Generate(step, scale, min, max, initial, false);

		public void Generate(float step, float scale, float min, float max, float factor, bool random)
		{
			lock (Sync)
			{
				if (step <= 0.0f) throw new ArgumentException(nameof(step));
				if (scale <= 0.0f) throw new ArgumentException(nameof(scale));
				if (min > -1.0f) throw new ArgumentException(nameof(step));
				if (max < +1.0f) throw new ArgumentException(nameof(step));

				if ((random && factor < 1.0f) || (!random && factor < min || max < factor)) throw new ArgumentException(nameof(step));

				Step = step;
				Scale = scale;
				Min = min;
				Max = max;
				Granularity = 0;

				var size = Size;

				Values?.Release();
				Values = new ComputeBuffer(size * size * size, sizeof(float));

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
				if (Counters == null) Counters = new ComputeBuffer(1, sizeof(uint));

				lock (TerrainInitializator)
				{
					var main = TerrainInitializator.FindKernel("main");

					TerrainInitializator.SetBuffer(main, "_values", Values);
					TerrainInitializator.SetFloat("_step", Step);
					TerrainInitializator.SetFloat("_scale", Scale);
					TerrainInitializator.SetFloat("_min", Min);
					TerrainInitializator.SetFloat("_max", Max);
					TerrainInitializator.SetFloat("_value", random ? -1.0f : factor);

					TerrainInitializator.Dispatch(main, size, size, size);
				}

				if (random) Randomize((int)factor);
			}
		}

		public void Clear()
		{
			lock (Sync)
			{
				if (Values == null) throw new InvalidOperationException("Terrain not generated. Call Generate() before this method.");

				lock (TerrainInitializator)
				{
					var main = TerrainInitializator.FindKernel("main");

					TerrainInitializator.SetBuffer(main, "_values", Values);
					TerrainInitializator.SetFloat("_step", Step);
					TerrainInitializator.SetFloat("_scale", Scale);
					TerrainInitializator.SetFloat("_min", Min);
					TerrainInitializator.SetFloat("_max", Max);
					TerrainInitializator.SetFloat("_value", -1.0f);

					var size = Size;
					TerrainInitializator.Dispatch(main, size, size, size);
				}
			}
		}

		public void Flatten(float height, float value)
		{
			lock (Sync)
			{
				if (Values == null) throw new InvalidOperationException("Terrain not generated. Call Generate() before this method.");
				if (height < 0.0f || Scale < height) throw new ArgumentException($"Ylimit must be in range [0.00, {Scale:F2}]");
				if (value < Min || Max < value) throw new ArgumentException($"Value must be in range [{Min}, {Max}].");
				
				lock (TerrainInitializator)
				{
					var main = TerrainInitializator.FindKernel("main");

					TerrainInitializator.SetBuffer(main, "_values", Values);
					TerrainInitializator.SetFloat("_step", Step);
					TerrainInitializator.SetFloat("_scale", Scale);
					TerrainInitializator.SetFloat("_min", Min);
					TerrainInitializator.SetFloat("_max", Max);
					TerrainInitializator.SetFloat("_value", value);

					var size = Size;
					TerrainInitializator.Dispatch(main, size, (int)Math.Ceiling(height / Step) + 1, size);
				}
			}
		}

		public void Randomize(int layers)
		{
			lock (Sync)
			{
				if (layers <= 0) throw new ArgumentException("Number of layers must be a positive number.");

				var delta_min = 1.0f + Min / 2.0f;
				var delta_max = 1.0f + Max / 2.0f;
				var layers_m1 = (float)Math.Max(layers - 1, 1);

				for (var l = 1; l <= layers; ++l)
				{
					var count = l * l;
					var factor = Scale / l;
					var halffactor = factor / 2.0f;
					var step = Scale / (l + 1);
					var halfstep = step / 2.0f;
					var layercap = (l - 1) / layers_m1;
					var ycorrect = (l - 1) * Scale / (2.5f * layers);
					var lfactor = Math.Min(l - 1.0f, 1.0f);
					if (ycorrect == 0.0f) ycorrect += 0.25f * Scale;
					
					for (var i = 0; i < count; ++i)
					{
						var center = new Vector3
						(
							step * (1 + i / l) + UnityEngine.Random.Range(-halfstep, +halfstep),
							ycorrect + -1.25f * factor + lfactor * UnityEngine.Random.Range(-halffactor, +halffactor),
							step * (1 + i % l) + UnityEngine.Random.Range(-halfstep, +halfstep)
						);

						var radius = factor + UnityEngine.Random.Range(-halffactor * lfactor, +halffactor);

						var delta = UnityEngine.Random.Range(delta_min * layercap, delta_max);

						Update(center, radius, delta);
					}
				}
			}
		}

		public void Update(Vector3 center, float radius, float delta)
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

		public void Gridify(int granularity)
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

				Granularity = granularity++;
				var targetCnt = granularity * granularity * granularity;
				var vertexCnt = Granularity * Granularity * Granularity * 15;

				Targets = new ComputeBuffer(targetCnt, 3 * sizeof(float));
				TargetValues = new ComputeBuffer(targetCnt, sizeof(float));

				Vertices = new ComputeBuffer(vertexCnt, 3 * sizeof(float));
				Indices = new ComputeBuffer(vertexCnt, sizeof(int));
				Normals = new ComputeBuffer(vertexCnt, 3 * sizeof(float));

				lock (Gridificator)
				{
					var main = Gridificator.FindKernel("main");

					Gridificator.SetBuffer(main, "_targets", Targets);
					Gridificator.SetInt("_granularity", Granularity);
					Gridificator.SetFloat("_scale", Scale);
					Gridificator.Dispatch(main, granularity, granularity, granularity);
				}
			}
		}

		public void Calculate()
		{
			lock (Sync)
			{
				if (Granularity == 0) throw new InvalidOperationException("Terrain not gridified. Call Gridify() before this method.");
				
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

		public void Triangulate()
		{
			lock (Sync)
			{
				if (Granularity == 0) throw new InvalidOperationException("Terrain not gridified. Call Gridify() before this method (for valid results Calculate() should also be called before this method).");
				
				var size = Granularity + 1;
				size = size * size * size;

				lock (Triangulator)
				{
					Counters.SetData(ResetCounters);

					var main = Triangulator.FindKernel("main");

					Triangulator.SetFloat("_step", Step);
					Triangulator.SetFloat("_scale", Scale);
					Triangulator.SetFloat("_min", Min);
					Triangulator.SetFloat("_max", Max);
					Triangulator.SetInt("_granularity", Granularity);
					Triangulator.SetInt("_target_count", size);
					Triangulator.SetBuffer(main, "_targets", Targets);
					Triangulator.SetBuffer(main, "_target_values", TargetValues);
					Triangulator.SetBuffer(main, "_vertices", Vertices);
					Triangulator.SetBuffer(main, "_indices", Indices);
					Triangulator.SetBuffer(main, "_normals", Normals);
					Triangulator.SetBuffer(main, "_counters", Counters);

					Triangulator.Dispatch(main, Granularity, Granularity, Granularity);
				}
			}
		}

		public void GetMeshData(out Vector3[] vertices, out int[] indices, out Vector3[] normals)
		{
			lock (Sync)
			{
				if (Granularity == 0) throw new InvalidOperationException("Cannot read terrain mesh data when none was generated. Call Gridify() before this method (for valid results Triangulate() should also be called before this method).");

				Counters.GetData(CountersCache);
				var count = (int)CountersCache[0];

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
				Counters?.Release();

				Values = null;
				Targets = null;
				TargetValues = null;
				Vertices = null;
				Indices = null;
				Normals = null;
				Counters = null;

				Granularity = 0;
			}
		}

		// [Unused]
		public void GetValues(float[] values)
		{
			lock (Sync)
			{
				if (Values == null) throw new InvalidOperationException("Terrain not generated. Call Generate() before this method.");
				if (values == null) throw new ArgumentNullException(nameof(values));
				if (values.Length != Values.count) throw new ArgumentException($"Array '{nameof(values)}' has invalid size.");
				
				Values.GetData(values);
			}
		}

		// [Unused]
		public void GetTargets(Vector3[] targets)
		{
			lock (Sync)
			{
				if (Targets == null) throw new InvalidOperationException("Terrain not gridified. Call Gridify() before this method.");
				if (targets == null) throw new ArgumentNullException(nameof(targets));
				if (targets.Length != Targets.count) throw new ArgumentException($"Array '{nameof(targets)}' has invalid size.");

				Targets.GetData(targets);
			}
		}

		// [Unused]
		public void GetTargetValues(float[] targetValues)
		{
			lock (Sync)
			{
				if (TargetValues == null) throw new InvalidOperationException("Terrain not gridified. Call Gridify() before this method (for valid results Calculate() should also be called before this method).");
				if (targetValues == null) throw new ArgumentNullException(nameof(targetValues));
				if (targetValues.Length != TargetValues.count) throw new ArgumentException($"Array '{nameof(targetValues)}' has invalid size.");

				TargetValues.GetData(targetValues);
			}
		}
	}
}
