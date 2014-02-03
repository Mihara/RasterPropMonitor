using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace JSI
{
	class JSIVesselView:InternalModule
	{
		[KSPField]
		public string backgroundColor = string.Empty;
		private Color backgroundColorValue = Color.black;
		[KSPField]
		public Vector4 viewDisplayPosition = new Vector4(0f, 0f, 512f, 512f);
		[KSPField]
		public bool useWireframe = false;

		private readonly Material lineMaterial = JUtil.DrawLineMaterial();
		private Matrix4x4 screenTransform = Matrix4x4.identity;
		private Matrix4x4 postfixTransform = Matrix4x4.identity;

		/// <summary>
		/// Draw the vessel view.
		/// </summary>
		/// <param name="screen"></param>
		/// <param name="cameraAspect"></param>
		/// <returns></returns>
		public bool RenderView(RenderTexture screen, float cameraAspect)
		{
			// MOARdV TODO: We can make this so the transform isn't updated
			// every frame by using a heuristic to wrap up the code from
			// here ...
			screenTransform = GetViewTransform();
			UpdateViewMatrix();
			//... to here, if it seems too expensive (do we have any real-time
			// timers available to measure this)?  On my system, it's not
			// causing any grief, but I've got a higher end system.

			Vector4 displayPosition = viewDisplayPosition;
			displayPosition.z = Mathf.Min(screen.width - displayPosition.x, displayPosition.z);
			displayPosition.w = Mathf.Min(screen.height - displayPosition.y, displayPosition.w);

			GL.Clear(true, true, backgroundColorValue);

			GL.PushMatrix();

			GL.LoadPixelMatrix(-displayPosition.z * 0.5f, displayPosition.z * 0.5f, -displayPosition.w * 0.5f, displayPosition.w * 0.5f);
			GL.Viewport(new Rect(displayPosition.x, screen.height - displayPosition.y - displayPosition.w, displayPosition.z, displayPosition.w));

			lineMaterial.SetPass(0);
			foreach(Part vesselPart in vessel.parts) {
				RenderPart(vesselPart);
			}
			GL.PopMatrix();

			return true;
		}

		/// <summary>
		/// Set up a view transformation (to place the camera direction
		/// relative to the vessel).
		/// </summary>
		/// <returns></returns>
		private Matrix4x4 GetViewTransform()
		{
			// We can alter the view position here.  The identity matrix
			// provides a view towards the XY plane, similar to the view in
			// the VAB.
			return Matrix4x4.identity;
		}

		/// <summary>
		/// Update the min/max values based on the transformed bounding box.
		/// </summary>
		/// <param name="bounds"></param>
		/// <param name="transform"></param>
		/// <param name="maxPos"></param>
		/// <param name="minPos"></param>
		private void UpdateMinMax(Bounds bounds, Matrix4x4 transform, ref Vector3 maxPos, ref Vector3 minPos)
		{
			var tmpVec = transform.MultiplyPoint3x4(new Vector3(bounds.min.x, bounds.min.y, bounds.min.z));
			maxPos = Vector3.Max(maxPos, tmpVec);
			minPos = Vector3.Min(minPos, tmpVec);

			tmpVec = transform.MultiplyPoint3x4(new Vector3(bounds.min.x, bounds.min.y, bounds.max.z));
			maxPos = Vector3.Max(maxPos, tmpVec);
			minPos = Vector3.Min(minPos, tmpVec);

			tmpVec = transform.MultiplyPoint3x4(new Vector3(bounds.min.x, bounds.max.y, bounds.min.z));
			maxPos = Vector3.Max(maxPos, tmpVec);
			minPos = Vector3.Min(minPos, tmpVec);

			tmpVec = transform.MultiplyPoint3x4(new Vector3(bounds.min.x, bounds.max.y, bounds.max.z));
			maxPos = Vector3.Max(maxPos, tmpVec);
			minPos = Vector3.Min(minPos, tmpVec);

			tmpVec = transform.MultiplyPoint3x4(new Vector3(bounds.max.x, bounds.min.y, bounds.min.z));
			maxPos = Vector3.Max(maxPos, tmpVec);
			minPos = Vector3.Min(minPos, tmpVec);

			tmpVec = transform.MultiplyPoint3x4(new Vector3(bounds.max.x, bounds.min.y, bounds.max.z));
			maxPos = Vector3.Max(maxPos, tmpVec);
			minPos = Vector3.Min(minPos, tmpVec);

			tmpVec = transform.MultiplyPoint3x4(new Vector3(bounds.max.x, bounds.max.y, bounds.min.z));
			maxPos = Vector3.Max(maxPos, tmpVec);
			minPos = Vector3.Min(minPos, tmpVec);

			tmpVec = transform.MultiplyPoint3x4(new Vector3(bounds.max.x, bounds.max.y, bounds.max.z));
			maxPos = Vector3.Max(maxPos, tmpVec);
			minPos = Vector3.Min(minPos, tmpVec);
		}

		/// <summary>
		/// Update the view matrices.
		/// </summary>
		/// <returns></returns>
		private void UpdateViewMatrix()
		{
			Vector3 maxPos = new Vector3(float.MinValue, float.MinValue, float.MinValue);
			Vector3 minPos = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);

			// Iterate over the parts
			foreach (Part vesselPart in vessel.parts) {
				var meshFilters = vesselPart.FindModelComponents<MeshFilter>();

				foreach (MeshFilter mesh in meshFilters) {
					if (mesh.renderer != null && mesh.renderer.gameObject != null && mesh.renderer.gameObject.activeInHierarchy) {
						Matrix4x4 localXform = vessel.vesselTransform.localToWorldMatrix.inverse * mesh.transform.localToWorldMatrix;
						// Is this right>!>
						localXform = localXform * screenTransform;
						UpdateMinMax(mesh.mesh.bounds, localXform, ref maxPos, ref minPos);
					}
				}
			}

			float xDisplacement = -0.5f * (maxPos.x + minPos.x);
			float yDisplacement = -0.5f * (maxPos.y + minPos.y);

			// Pad the values a little so we don't draw all the way onto the
			// edge.
			float neededWidth = 1.05f * (maxPos.x - minPos.x);
			float neededHeight = 1.05f * (maxPos.y - minPos.y);

			float pixelScalar = Mathf.Min(viewDisplayPosition.z / neededWidth, viewDisplayPosition.w / neededHeight);

			postfixTransform = Matrix4x4.Scale(new Vector3(pixelScalar, pixelScalar, pixelScalar));
			postfixTransform[0, 3] = xDisplacement * pixelScalar;
			postfixTransform[1, 3] = yDisplacement * pixelScalar;
		}

		/// <summary>
		/// Render a part.
		/// </summary>
		/// <param name="vesselPart"></param>
		private void RenderPart(Part vesselPart)
		{
			var meshFilters = vesselPart.FindModelComponents<MeshFilter>();

			foreach (MeshFilter mesh in meshFilters) {
				if(mesh.renderer != null && mesh.renderer.gameObject != null && mesh.renderer.material != null)
				if(mesh.renderer.gameObject.activeInHierarchy) {
					Matrix4x4 localXform = vessel.vesselTransform.localToWorldMatrix.inverse * mesh.transform.localToWorldMatrix;
					localXform = postfixTransform * (localXform * screenTransform);
					if (useWireframe) {
						DrawWireframe(mesh.mesh.triangles, mesh.mesh.vertices, localXform);
					} else {
						DrawTriangles(mesh.mesh.triangles, mesh.mesh.vertices, localXform);
					}
				}
			}
		}

		/// <summary>
		/// Technically, we could set GL to wireframe mode and call GL.Begin
		/// with triangles, but that is less performant than simply calling
		/// GL.Begin and decomposing the model into lines.
		/// </summary>
		private void DrawWireframe(int[] indices, Vector3[] vertices, Matrix4x4 transform)
		{
			GL.Color(Color.white);
			GL.Begin(GL.LINES);
			for (int i = 0; i < indices.Length; i += 3) {
				var vtx1 = transform.MultiplyPoint3x4(vertices[indices[i]]);
				GL.Vertex3(vtx1.x, vtx1.y, 0.0f);

				var vtx = transform.MultiplyPoint3x4(vertices[indices[i + 1]]);
				GL.Vertex3(vtx.x, vtx.y, 0.0f);
				GL.Vertex3(vtx.x, vtx.y, 0.0f);

				vtx = transform.MultiplyPoint3x4(vertices[indices[i + 2]]);
				GL.Vertex3(vtx.x, vtx.y, 0.0f);
				GL.Vertex3(vtx.x, vtx.y, 0.0f);

				GL.Vertex3(vtx1.x, vtx1.y, 0.0f);
			}
			GL.End();
		}

		/// <summary>
		/// Render using filled triangles
		/// </summary>
		/// <param name="indices"></param>
		/// <param name="vertices"></param>
		/// <param name="transform"></param>
		private void DrawTriangles(int[] indices, Vector3[] vertices, Matrix4x4 transform)
		{
			GL.Color(Color.white);
			GL.Begin(GL.TRIANGLES);
			for (int i = 0; i < indices.Length; i += 3) {
				var vtx = transform.MultiplyPoint3x4(vertices[indices[i]]);
				GL.Vertex3(vtx.x, vtx.y, 0.0f);

				vtx = transform.MultiplyPoint3x4(vertices[indices[i + 1]]);
				GL.Vertex3(vtx.x, vtx.y, 0.0f);

				vtx = transform.MultiplyPoint3x4(vertices[indices[i + 2]]);
				GL.Vertex3(vtx.x, vtx.y, 0.0f);
			}
			GL.End();
		}

		/// <summary>
		/// Parse configuration strings.
		/// </summary>
		public void Start()
		{
			if (!string.IsNullOrEmpty(backgroundColor)) {
				backgroundColorValue = ConfigNode.ParseColor32(backgroundColor);
			}
		}
	}
}
