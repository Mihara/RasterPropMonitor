using System;
using UnityEngine;
using System.Collections.Generic;

namespace JSI
{
	public class JSIVariableGraph: InternalModule
	{
		private readonly List<GraphLine> graphs = new List<GraphLine>();
		private RasterPropMonitorComputer comp;

		public void Start()
		{
			comp = JUtil.GetComputer(internalProp);

			// Unexpected stumbling block: I can't get at blocks within a BACKGROUNDHANDLER block
			// using my GameDatabase trick because I don't really know which page instantiated this module.
			// I need some other method... Probably, a global GameData named block and an in-handler-block
			// reference by name.

		}

		public bool RenderGraphs(RenderTexture screen, float cameraAspect)
		{
			return false;
		}

		public override void OnUpdate()
		{
			double time = Planetarium.GetUniversalTime();
			foreach (GraphLine graph in graphs) {
				graph.Update(time);
			}
		}

		private class GraphLine
		{
			private readonly Color32 lineColor;
			private readonly List<Vector2d> points = new List<Vector2d>();
			private readonly int trailLimit;
			private readonly string variableName;
			private readonly RasterPropMonitorComputer comp;

			public GraphLine(ConfigNode node, double xSpan, double xResolution, RasterPropMonitorComputer compInstance)
			{
				comp = compInstance;

				trailLimit = (int)(xSpan / xResolution);
				if (!node.HasData)
					throw new ArgumentException("Graph block with no data?");
				if (node.HasValue("variableName")) {
					variableName = node.GetValue("variableName");
				} else
					throw new ArgumentException("Draw a graph of what?");

				lineColor = Color.white;
				if (node.HasValue("color"))
					lineColor = ConfigNode.ParseColor(node.GetValue("color"));
			}

			public void Draw(Rect screenRect)
			{
			}

			public void Update(double time)
			{

				if (trailLimit > 0) {
					points.Add(new Vector2d(time, JUtil.MassageObjectToDouble(comp.ProcessVariable(variableName))));
					if (points.Count > trailLimit)
						points.RemoveRange(0, points.Count - trailLimit);
				}

			}
		}
	}
}

