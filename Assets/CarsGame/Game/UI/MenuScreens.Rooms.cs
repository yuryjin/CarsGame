using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RacingGame
{
    public sealed partial class MenuScreens
    {
        private string roomDigits = "";

        private void BuildRooms(RectTransform frame)
        {
            var link = GameFlow.Room;
            var content = UiFactory.Scroll(Header(frame,"room.title",showWallet:false),"RoomScroll",Vector2.zero,Vector2.zero);
            if (link == null) return;
            void Button(string name, string key, Action action) => UiFactory.Button(Item(content,name+"Row",78f,false),name,
                Loc.Get(key),action,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,27f,UiFactory.Raised);
            if (link.State == RoomState.Connecting) { Section(content,"RoomStatus",Loc.Get("common.loading")); return; }
            if (link.State == RoomState.Lobby)
            {
                int count = 0;
                for (int i = 0; i < 4; i++) if ((link.Members & (1 << i)) != 0) count++;
                Section(content,"RoomRoster",Loc.Format("room.lobby","n",count));
                var field = Item(content,"RoomInvite",75f);
                var code = LineInput(field, 8, link.Code.ToString(), 26f);
                code.readOnly = true;
                Section(content,"RoomMode",Loc.Get(link.Fortress ? "room.fortress" : "room.race"));
                if (link.Seat == 0) Button("RoomStart","menu.play",count > 1 ? link.StartMatch : (Action)null);
                return;
            }
            if (link.State == RoomState.Results)
            {
                Section(content,"RoomResult",Loc.Get(link.Outcome == 0 ? "results.title" : link.Outcome == 1 ? "mission.victory" : "mission.defeat"));
                for (int i = 0; i < link.FinishOrder.Length; i++)
                {
                    int seat = link.FinishOrder[i];
                    string name = Loc.Format((link.Members & (1 << seat)) != 0 ? "room.driver" : "room.bot","n",seat+1);
                    if (!link.Fortress && (link.FinishedSeats & (1 << seat)) == 0) name += " · " + Loc.Get("results.dnf");
                    Section(content,"RoomPlace"+i,(i+1) + ".  " + name);
                }
                return;
            }
            if (link.State == RoomState.Interrupted || link.State == RoomState.Unavailable)
                Section(content,"RoomStatus",Loc.Get(link.State == RoomState.Interrupted ? "room.interrupted" : "room.unavailable"));
            Button("CreateRaceRoom","room.race",() => link.Create(false));
            Button("CreateCoopRoom","room.fortress",() => link.Create(true));
            Section(content,"RoomCodeLabel",Loc.Get("room.code"));
            var input = LineInput(Item(content, "RoomCode", 75f), 8, roomDigits, 26f);
            input.characterValidation = TMP_InputField.CharacterValidation.Digit;
            input.onValueChanged.AddListener(value => roomDigits = value);
            Button("JoinRoom","room.join",() => { uint.TryParse(roomDigits,out uint code); link.Join(code); });
        }
    }
}
