namespace SlimeDungeon.Core;

public interface IScreen
{
    void OnEnter(GameContext ctx) { }
    void OnExit(GameContext ctx) { }
    void Update(GameContext ctx, float dt);
    void Draw(GameContext ctx);
}
