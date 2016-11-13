﻿//   HangarMachineryEditor.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System.Collections.Generic;
using UnityEngine;
using KSP.UI.Screens;
using AT_Utils;

namespace AtHangar
{
	public partial class HangarMachinery
	{
		#region In-Editor Content Management
		const int windows_width = 400;
		const string eLock  = "Hangar.EditHangar";
		const string scLock = "Hangar.LoadShipConstruct";
		enum EditorWindows { None, EditContent, EditName, RelocateVessels }
		readonly Multiplexer<EditorWindows> selected_window = new Multiplexer<EditorWindows>();

		Vector2 constructs_scroll = Vector2.zero;
		Vector2 unfit_scroll = Vector2.zero;
		Rect eWindowPos  = new Rect(Screen.width/2-windows_width/2, 100, windows_width, 100);
		Rect neWindowPos = new Rect(Screen.width/2-windows_width/2, 100, windows_width, 50);

		readonly VesselTransferWindow vessels_window = new VesselTransferWindow();
		CraftBrowserDialog vessel_selector;
		EditorFacility facility;


		IEnumerator<YieldInstruction> delayed_try_store_construct(PackedConstruct pc)
		{
			if(pc.construct == null) yield break;
			Utils.LockEditor(scLock);
			for(int i = 0; i < 3; i++) yield return null;
			pc.UpdateMetric(Storage.ComputeHull);
			try_store_vessel(pc);
			pc.UnloadConstruct();
			Utils.LockEditor(scLock, false);
		}

		void vessel_selected(string filename, CraftBrowserDialog.LoadType t)
		{
			EditorLogic EL = EditorLogic.fetch;
			if(EL == null) return;
			//load vessel config
			vessel_selector = null;
			var pc = new PackedConstruct(filename, HighLogic.CurrentGame.flagURL);
			if(pc.construct == null) 
			{
				Utils.Log("PackedConstruct: unable to load ShipConstruct from {}. " +
				          "This usually means that some parts are missing " +
				          "or some modules failed to initialize.", filename);
				Utils.Message("Unable to load {0}", filename);
				return;
			}
			//check if the construct contains launch clamps
			if(pc.construct.HasLaunchClamp())
			{
				Utils.Message("\"{0}\" has launch clamps. Remove them before storing.", pc.name);
				pc.UnloadConstruct();
				return;
			}
			//check if it's possible to launch such vessel
			bool cant_launch = false;
			var preFlightCheck = new PreFlightCheck(new Callback(() => cant_launch = false), new Callback(() => cant_launch = true));
			preFlightCheck.AddTest(new PreFlightTests.ExperimentalPartsAvailable(pc.construct));
			preFlightCheck.RunTests(); 
			//cleanup loaded parts and try to store construct
			if(cant_launch) pc.UnloadConstruct();
			else StartCoroutine(delayed_try_store_construct(pc));
		}
		void selection_canceled() { vessel_selector = null; }

		void hangar_content_editor(int windowID)
		{
			GUILayout.BeginVertical();
			GUILayout.BeginHorizontal();
			//Vessel selector
			if(GUILayout.Button("Select Vessel", Styles.normal_button, GUILayout.ExpandWidth(true))) 
				vessel_selector = 
					CraftBrowserDialog.Spawn(
						facility,
						HighLogic.SaveFolder,
						vessel_selected,
						selection_canceled, false);
			GUILayout.EndHorizontal();
			//hangar info
			if(ConnectedStorage.Count > 1)
				HangarGUI.UsedVolumeLabel(TotalUsedVolume, TotalUsedVolumeFrac, "Total Used Volume");
			HangarGUI.UsedVolumeLabel(UsedVolume, UsedVolumeFrac);
			//hangar contents
			var constructs = Storage.GetConstructs();
			constructs.Sort((a, b) => a.name.CompareTo(b.name));
			constructs_scroll = GUILayout.BeginScrollView(constructs_scroll, GUILayout.Height(200), GUILayout.Width(windows_width));
			GUILayout.BeginVertical();
			foreach(PackedConstruct pc in constructs)
			{
				GUILayout.BeginHorizontal();
				HangarGUI.PackedVesselLabel(pc);
				if(GUILayout.Button("+1", Styles.green_button, GUILayout.Width(25))) 
					try_store_vessel(pc.Clone());
				if(GUILayout.Button("X", Styles.red_button, GUILayout.Width(25))) 
					Storage.RemoveVessel(pc);
				GUILayout.EndHorizontal();
			}
			GUILayout.EndVertical();
			GUILayout.EndScrollView();
			//unfit constructs
			constructs = Storage.UnfitConstucts;
			if(constructs.Count > 0)
			{
				GUILayout.Label("Unfit vessels:", Styles.yellow, GUILayout.ExpandWidth(true));
				unfit_scroll = GUILayout.BeginScrollView(unfit_scroll, GUILayout.Height(100), GUILayout.Width(windows_width));
				GUILayout.BeginVertical();
				foreach(PackedConstruct pc in Storage.UnfitConstucts)
				{
					GUILayout.BeginHorizontal();
					HangarGUI.PackedVesselLabel(pc);
					if(GUILayout.Button("^", Styles.green_button, GUILayout.Width(25))) 
					{ if(try_store_vessel(pc.Clone())) Storage.RemoveUnfit(pc); }
					if(GUILayout.Button("X", Styles.red_button, GUILayout.Width(25))) 
						Storage.RemoveUnfit(pc);
					GUILayout.EndHorizontal();
				}
				GUILayout.EndVertical();
				GUILayout.EndScrollView();
			}
			//common buttons
			if(GUILayout.Button("Clear", Styles.red_button, GUILayout.ExpandWidth(true)))
				Storage.ClearConstructs();
			if(GUILayout.Button("Close", Styles.normal_button, GUILayout.ExpandWidth(true))) 
			{
				Utils.LockEditor(eLock, false);
				selected_window.Off();
			}
			GUILayout.EndVertical();
			GUI.DragWindow(new Rect(0, 0, Screen.width, 20));
		}

		void hangar_name_editor(int windowID)
		{
			GUILayout.BeginVertical();
			HangarName = GUILayout.TextField(HangarName, 50);
			if(GUILayout.Button("Close", Styles.normal_button, GUILayout.ExpandWidth(true))) 
			{
				Utils.LockEditor(eLock, false);
				selected_window.Off();
			}
			GUILayout.EndVertical();
			GUI.DragWindow(new Rect(0, 0, Screen.width, 20));
		}

		public void OnGUI() 
		{ 
			if(Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint) return;
			if(!selected_window) return;
			Styles.Init();
			//edit hangar
			if(selected_window[EditorWindows.EditContent])
			{
				if(vessel_selector == null) 
				{
					Utils.LockIfMouseOver(eLock, eWindowPos);
					eWindowPos = GUILayout.Window(GetInstanceID(), eWindowPos,
												  hangar_content_editor,
												  "Hangar Contents Editor",
												  GUILayout.Width(windows_width),
					                              GUILayout.Height(300)).clampToScreen();
				}
			}
			//edit name
			else if(selected_window[EditorWindows.EditName])
			{
				Utils.LockIfMouseOver(eLock, neWindowPos);
				neWindowPos = GUILayout.Window(GetInstanceID(), neWindowPos,
				                               hangar_name_editor,
				                               "Rename Hangar",
				                               GUILayout.Width(windows_width)).clampToScreen();
			}
			else if(selected_window[EditorWindows.RelocateVessels])
			{
				vessels_window.Draw(ConnectedStorage, GetInstanceID());
				vessels_window.TransferVessel();
				if(vessels_window.Closed) RelocateVessels();
			}
		}

		[KSPEvent (guiActive = true, guiActiveEditor = true, guiName = "Rename Hangar", active = true)]
		public void EditName() 
		{ 
			selected_window.Toggle(EditorWindows.EditName);
			Utils.LockIfMouseOver(eLock, neWindowPos, selected_window[EditorWindows.EditName]);
		}

		[KSPEvent (guiActiveEditor = true, guiName = "Edit contents", active = true)]
		public void EditHangar() 
		{ 
			if(!HighLogic.LoadedSceneIsEditor) return;
			selected_window.Toggle(EditorWindows.EditContent);
			Utils.LockIfMouseOver(eLock, eWindowPos, selected_window[EditorWindows.EditContent]);
		}

		[KSPEvent (guiActiveEditor = true, guiName = "Relocate vessels", active = true)]
		public void RelocateVessels() 
		{ 
			selected_window.Toggle(EditorWindows.RelocateVessels);
			if(!selected_window[EditorWindows.RelocateVessels]) 
			{ vessels_window.ClearSelection(); vessels_window.UnlockControls(); }
		}
		#endregion

		#if DEBUG
		void OnRenderObject()
		{
			if(vessel != null)
			{
				if(vessel != FlightGlobals.ActiveVessel)
				{
					Utils.GLDrawPoint(vessel.transform.position, Color.red);
					Utils.GLDrawPoint(vessel.CoM, Color.green);
				}
//				Utils.GLLine(vessel.transform.position, vessel.orbit.getRelativePositionAtUT(Planetarium.GetUniversalTime()+TimeWarp.fixedDeltaTime).xzy+vessel.mainBody.position, Color.magenta);
//				Utils.GLVec(vessel.transform.position,  vessel.orbit.GetRotFrameVel(vessel.mainBody).xzy*TimeWarp.fixedDeltaTime, Color.blue);	
				Utils.GLVec(part.transform.position+part.transform.TransformDirection(part.CoMOffset), momentumDelta, Color.red);
			}
			if(launched_vessel != null && launched_vessel.vessel != null)
			{
				Utils.GLDrawPoint(launched_vessel.vessel.transform.position, Color.yellow);
				Utils.GLLine(launched_vessel.vessel.transform.position, vessel.transform.position, Color.yellow);
				Utils.GLVec(launched_vessel.vessel.transform.position, part.Rigidbody.velocity, Color.red);
				Utils.GLVec(launched_vessel.vessel.transform.position, launched_vessel.dV, Color.cyan);
			}
//			if(selected_window[EditorWindows.EditContent] && Storage != null)
//			{
//				PackedVessel vsl = null;
//				if(Storage.ConstructsCount > 0) vsl = Storage.GetConstructs()[0];
//				else if(Storage.UnfitCount > 0) vsl = Storage.UnfitConstucts[0];
//				if(vsl != null)
//				{
//					var metric = vsl.metric;
//					var hull = metric.hull;
//					Utils.DrawPoint(Vector3.zero, Storage.spawn_transform, Color.red);
//					if(hull != null) Utils.GLDrawHull(hull, get_spawn_transform(vsl), metric.center-Storage.GetSpawnOffset(vsl), Color.green, false);
//				}
//				if(Storage.hangar_space != null)
//					Utils.GLDrawMesh(Storage.hangar_space.sharedMesh, Storage.hangar_space.transform, c:Color.cyan, filled:false);
//			}
//			foreach(var dc in part.DragCubes.Cubes)
//				Utils.GLDrawBounds(new Bounds(dc.Center, dc.Size), part.transform, Color.yellow*dc.Weight);
		}
		#endif
	}
}

