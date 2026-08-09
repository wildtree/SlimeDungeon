using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Domain;
using SlimeDungeon.Graphics;
using SlimeDungeon.UI;

namespace SlimeDungeon.Guild;

public sealed class NamingScreen : IScreen
{
    private enum Phase { Input, Gender, Confirm }

    private Phase _phase = Phase.Input;
    private string _name = "";
    private Gender _gender = Gender.Male;

    public void OnEnter(GameContext ctx)
    {
        ctx.Input.ClearTextInput();
        SDL.StartTextInput(ctx.Window);
    }

    public void OnExit(GameContext ctx) => SDL.StopTextInput(ctx.Window);

    public void Update(GameContext ctx, float dt)
    {
        var input = ctx.Input;

        if (_phase == Phase.Confirm)
        {
            if (MenuNav.Cancelled(input))
            {
                _phase = Phase.Input;
                return;
            }
            if (MenuNav.Confirmed(input))
            {
                var player = Player.CreateNew(_name.Trim(), _gender);
                player.OpenQuests = QuestFactory.CreateInitialQuests(player.DayCount);
                ctx.Player = player;
                ctx.Screens.ChangeTo(new GuildScreen());
            }
            return;
        }

        if (_phase == Phase.Gender)
        {
            if (MenuNav.Left(input) || MenuNav.Right(input))
            {
                _gender = _gender == Gender.Male ? Gender.Female : Gender.Male;
            }
            else if (MenuNav.Cancelled(input))
            {
                _phase = Phase.Input;
                input.ClearTextInput();
            }
            else if (MenuNav.Confirmed(input))
            {
                _phase = Phase.Confirm;
                input.ClearTextInput();
            }
            return;
        }

        while (input.TryDequeueChar(out var c))
        {
            if (_name.Length < 10 && !char.IsControl(c))
                _name += c;
        }

        if (input.WasPressed(SDL.Keycode.Backspace) && _name.Length > 0)
            _name = _name[..^1];

        if (input.TextEntryConfirmed() && _name.Trim().Length > 0)
            _phase = Phase.Gender;
    }

    public void Draw(GameContext ctx)
    {
        var r = ctx.Renderer;
        r.Clear(Colors.Rgb(24, 20, 16));
        var fonts = ctx.Fonts;

        // No character exists yet, so the room's date board shows today.
        GuildRoom.Draw(ctx, GameCalendar.Today());

        // Registration form, boxed over the dark area to the right of the illustration.
        const float panelX = 416f;
        r.FillRect(panelX - 8, 12, 216, 276, Colors.Rgb(20, 16, 12, 210));
        r.DrawRect(panelX - 8, 12, 216, 276, Colors.Border);

        fonts.DrawText(r.Handle, "冒険者登録", panelX, 20, 16, Colors.White);

        fonts.DrawText(r.Handle, "名前:", panelX, 60, 12, Colors.Highlight);
        r.FillRect(panelX, 78, 192, 22, Colors.PanelBg);
        r.DrawRect(panelX, 78, 192, 22, Colors.Border);
        var nameDisplay = _phase == Phase.Input ? _name + "_" : _name;
        fonts.DrawText(r.Handle, nameDisplay, panelX + 6, 81, 14, Colors.White);

        if (_phase != Phase.Input)
        {
            fonts.DrawText(r.Handle, "性別:", panelX, 114, 12, Colors.Highlight);
            var genderLabels = new[] { "男", "女" };
            var genderMaxWidth = MenuNav.MaxLabelWidth(ctx, genderLabels, 14);
            MenuNav.DrawRow(ctx, panelX, 132, genderMaxWidth, 20, genderLabels[0], 14, _gender == Gender.Male);
            MenuNav.DrawRow(ctx, panelX + genderMaxWidth + 10, 132, genderMaxWidth, 20, genderLabels[1], 14, _gender == Gender.Female);
        }
        var sprite = ctx.Sprites.PlayerSprite(_gender, Direction.Down, WalkFrame.A);
        r.DrawTexture(sprite, panelX + 72, 200, 48, 48);

        if (_phase == Phase.Input)
        {
            fonts.DrawText(r.Handle, "文字を入力してください", panelX, 258, 10, Colors.Border);
            //fonts.DrawText(r.Handle, "（左右キーで性別変更）", panelX, 272, 10, Colors.Border);
        }
        else if (_phase == Phase.Gender)
        {
            fonts.DrawText(r.Handle, "左右キーで性別変更", panelX, 258, 10, Colors.Border);
        }
        else
        {
            ControlHints.Draw(ctx, panelX, 262, 10, Colors.Border, ControlHints.Confirm("決定"), ControlHints.Cancel("戻る"));
        }

        // Receptionist's dialogue, in a speech-box across the bottom of the whole scene.
        const float boxX = 16f, boxY = 306f, boxW = 608f, boxH = 78f;
        r.FillRect(boxX, boxY, boxW, boxH, Colors.Rgb(20, 16, 12, 230));
        r.DrawRect(boxX, boxY, boxW, boxH, Colors.Border);
        fonts.DrawText(r.Handle, "受付嬢", boxX + 12, boxY + 8, 12, Colors.Highlight);

        var line = "冒険者登録ですね？ お名前を教えてください。";
        switch (_phase)
        {
            case Phase.Gender:
                line = "性別を教えてください。";
                break;
            case Phase.Confirm:
                line = $"{_name.Trim()}様({(_gender == Gender.Male ? "男性" : "女性")})ですね。これでHランク冒険者として登録してよろしいですか？";
                break;
        }
        fonts.DrawText(r.Handle, line, boxX + 12, boxY + 34, 13, Colors.White);
    }
}
