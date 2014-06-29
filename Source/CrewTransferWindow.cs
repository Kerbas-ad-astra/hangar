using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AtHangar
{
	public class CrewTransferWindow : MonoBehaviour
	{
		private int CrewCapacity;
		private List<ProtoCrewMember> crew;
		private List<ProtoCrewMember> selected;
		
		public CrewTransferWindow() { Styles.Init(); }
		
        private Vector2 crew_scroll_view = Vector2.zero;
        private void TransferWindow(int windowId)
        {
            crew_scroll_view = GUILayout.BeginScrollView(crew_scroll_view, GUILayout.Height(200), GUILayout.Width(300));
            GUILayout.BeginVertical();
            foreach(ProtoCrewMember kerbal in crew)
            {
                GUILayout.BeginHorizontal();
				int ki = selected.FindIndex(cr => cr.name == kerbal.name);
				GUIStyle style = (ki >= 0) ? Styles.green : Styles.normal;
				GUILayout.Label(kerbal.name, style, GUILayout.Width(200));
				if(ki >= 0)
                {
                    if(GUILayout.Button("Selected", style, GUILayout.Width(70)))
						selected.RemoveAt(ki);
                }
				else if(selected.Count < CrewCapacity)
				{
					if(GUILayout.Button("Select", GUILayout.Width(60)))
						selected.Add(kerbal);
				}
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, Screen.width, 30));
        }
		
		public Rect Draw(List<ProtoCrewMember> _crew, 
		                 List<ProtoCrewMember> _selected, 
		                 int _crew_capacity, 
		                 Rect windowPos)
		{
			crew = _crew;
			selected = _selected;
			CrewCapacity = _crew_capacity;
			Debug.Log("[Hangar] selected crew:\n"+Utils.formatCrewList(selected));
			windowPos = GUILayout.Window(GetInstanceID(), 
										 windowPos, TransferWindow,
										 string.Format("Vessel Crew {0}/{1}", selected.Count, CrewCapacity),
										 GUILayout.Width(260));
			return windowPos;
		}
	}
}

