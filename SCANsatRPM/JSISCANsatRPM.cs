// Mental note:
// It is not entirely clear which .NET version should KSP plugins be compiled for,
// but the consensus is that 3.5 is the most appropriate because types introduced
// in 4.0 can be verified not to work. It is a fact that you can use C#4 itself
// with it with no ill effects, though -- at least all the features which rely
// on the compiler, rather than on the libraries.
// SCANsat is compiled for .NET 4.0 for some reason, which means that
// this assembly also needs to be compiled for 4.0 to link to it. Which can and probably will
// cause problems.
// I wish there were some clarity on the subject.
using SCANsat;
using UnityEngine;
using System;

namespace SCANsatRPM
{
	public class JSISCANsatRPM: InternalModule
	{
		[KSPField]
		public int buttonUp;
		[KSPField]
		public int buttonDown = 1;
		[KSPField]
		public int buttonEnter = 2;
		[KSPField]
		public int buttonEsc = 3;
		[KSPField]
		public int maxZoom = 20;
		[KSPField]
		public float iconPixelSize = 8f;
		[KSPField]
		public Vector2 iconShadowShift = new Vector2(1, 1);
		[KSPField]
		public float redrawEdge = 0.8f;
		[KSPField]
		public Color iconColorSelf = Color.white;
		[KSPField]
		public Color iconColorTarget = Color.yellow;
		[KSPField]
		public Color iconColorUnvisitedAnomaly = Color.red;
		[KSPField]
		public Color iconColorVisitedAnomaly = Color.green;
		[KSPField]
		public Color iconColorShadow = Color.black;
		[KSPField]
		public float zoomModifier;
		private int mapMode;
		private int zoomLevel = 1;
		private int screenWidth;
		private int screenHeight;
		private double mapCenterLong, mapCenterLat;
		private SCANmap map;
		private CelestialBody orbitingBody;
		private Vessel targetVessel;
		private double redrawDeviation;
		private SCANdata.SCANanomaly[] localAnomalies;
		private Material iconMaterial;

		public bool MapRenderer(RenderTexture screen)
		{
			// Just in case.
			if (!HighLogic.LoadedSceneIsFlight)
				return false;

			if (screenWidth == 0 || screenHeight == 0) {
				screenWidth = screen.width;
				screenHeight = screen.height;
				InitMap();
				return false;
			}

			Graphics.Blit(map.map, screen);
			GL.PushMatrix();
			GL.LoadPixelMatrix(0, screenWidth, screenHeight, 0);
			foreach (SCANdata.SCANanomaly anomaly in localAnomalies) {
				if (anomaly.known)
					DrawIcon(anomaly.longitude, anomaly.latitude,
						anomaly.detail ? (VesselType)int.MaxValue : VesselType.Unknown,
						anomaly.detail ? iconColorVisitedAnomaly : iconColorUnvisitedAnomaly);
			}
			if (targetVessel != null && targetVessel.mainBody == orbitingBody)
				DrawIcon(targetVessel.longitude, targetVessel.latitude, targetVessel.vesselType, iconColorTarget);
			DrawIcon(vessel.longitude, vessel.latitude, vessel.vesselType, iconColorSelf);
			GL.PopMatrix();

			return true;
		}

		private void DrawIcon(double longitude, double latitude, VesselType vt, Color iconColor)
		{
			var position = new Rect((float)(rescaleLongitude((map.projectLongitude(longitude, latitude) + 180) % 360) * screenWidth / 360 - iconPixelSize / 2),
				               (float)(screenHeight - (rescaleLatitude((map.projectLatitude(longitude, latitude) + 90) % 180) * screenHeight / 180) - iconPixelSize / 2),
				               iconPixelSize, iconPixelSize);

			Rect shadow = position;
			shadow.x += iconShadowShift.x;
			shadow.y += iconShadowShift.y;

			iconMaterial.color = iconColorShadow;
			Graphics.DrawTexture(shadow, MapView.OrbitIconsMap, VesselTypeIcon(vt), 0, 0, 0, 0, iconMaterial);

			iconMaterial.color = iconColor;
			Graphics.DrawTexture(position, MapView.OrbitIconsMap, VesselTypeIcon(vt), 0, 0, 0, 0, iconMaterial);
		}

		private double rescaleLatitude(double lat)
		{
			lat = Clamp(lat - map.lat_offset, 180);
			lat *= 180f / (map.mapheight / map.mapscale);
			return lat;
		}

		private double rescaleLongitude(double lon)
		{
			lon = Clamp(lon - map.lon_offset, 360);
			lon *= 360f / (map.mapwidth / map.mapscale);
			return lon;
		}

		private static double Clamp(double value, double clamp)
		{
			value = value % clamp;
			if (value < 0)
				return value + clamp;
			return value;
		}

		private static Rect VesselTypeIcon(VesselType type)
		{
			int x, y;
			const float symbolSpan = 0.2f;
			switch (type) {
				case VesselType.Base:
					x = 2;
					y = 0;
					break;
				case VesselType.Debris:
					x = 1;
					y = 3;
					break;
				case VesselType.EVA:
					x = 2;
					y = 2;
					break;
				case VesselType.Flag:
					x = 4;
					y = 0;
					break;
				case VesselType.Lander:
					x = 3;
					y = 0;
					break;
				case VesselType.Probe:
					x = 1;
					y = 0;
					break;
				case VesselType.Rover:
					x = 0;
					y = 0;
					break;
				case VesselType.Ship:
					x = 0;
					y = 3;
					break;
				case VesselType.Station:
					x = 3;
					y = 1;
					break;
				case VesselType.Unknown:
					x = 3;
					y = 3;
					break;
				default:
					x = 3;
					y = 2;
					break;
			}
			var result = new Rect();
			result.x = symbolSpan * x;
			result.y = symbolSpan * y;
			result.height = result.width = symbolSpan;
			return result;
		}

		public void ButtonProcessor(int buttonID)
		{
			if (screenWidth == 0 || screenHeight == 0)
				return;
			if (buttonID == buttonUp) {
				ChangeZoom(false);
			}
			if (buttonID == buttonDown) {
				ChangeZoom(true);
			}
			if (buttonID == buttonEnter) {
				ChangeMapMode(true);
			}
			if (buttonID == buttonEsc) {
				// Whatever possessed him to do THAT?
				SCANcontroller.controller.colours = SCANcontroller.controller.colours == 0 ? 1 : 0;
				RedrawMap();
			}
		}

		private void ChangeMapMode(bool up)
		{
			mapMode += up ? 1 : -1;

			if (mapMode > 2)
				mapMode = 0;
			if (mapMode < 0)
				mapMode = 2;

			map.resetMap(mapMode);
		}

		private void ChangeZoom(bool up)
		{
			int oldZoom = zoomLevel;
			zoomLevel += up ? 1 : -1;
			if (zoomLevel < 1)
				zoomLevel = 1;
			if (zoomLevel > maxZoom)
				zoomLevel = maxZoom;
			if (zoomLevel != oldZoom) {
				RedrawMap();
			}
		}

		public override void OnUpdate()
		{
			if (!HighLogic.LoadedSceneIsFlight || vessel != FlightGlobals.ActiveVessel)
				return;

			if (!(CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
			    CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal))
				return;

			if (map != null && !map.isMapComplete()) {
				map.getPartialMap();
			}

			targetVessel = FlightGlobals.fetch.VesselTarget as Vessel;

			if (UpdateCheck() || orbitingBody != vessel.mainBody)
				RedrawMap();
		}

		private void RedrawMap()
		{
			orbitingBody = vessel.mainBody;
			map.setBody(vessel.mainBody);
			map.setSize(screenWidth, screenHeight);
			map.mapscale *= (zoomLevel + zoomModifier);
			mapCenterLong = vessel.longitude;
			mapCenterLat = vessel.latitude;
			map.centerAround(mapCenterLong, mapCenterLat);
			map.resetMap(mapMode);
			redrawDeviation = redrawEdge * 180 / (zoomLevel + zoomModifier);
			localAnomalies = SCANcontroller.controller.getData(vessel.mainBody).getAnomalies();
		}

		private bool UpdateCheck()
		{
			if (map == null)
				return false;
			if ((Math.Abs(vessel.latitude - mapCenterLat) > redrawDeviation) ||
			    (Math.Abs(vessel.longitude - mapCenterLong) > redrawDeviation))
				return true;

			return false;
		}

		private void InitMap()
		{
			iconMaterial = new Material(Shader.Find("KSP/Alpha/Unlit Transparent"));
			map = new SCANmap();
			map.setProjection(SCANmap.MapProjection.Rectangular);
			RedrawMap();
		}
	}
}

