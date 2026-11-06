// handles Save player / load player
// handles save inventory / load inventory
// handles save world state / load world state

public class GameRepository
{
    private readonly Database _database;

    public GameRepository(Database database)
    {
        _database = database;
    }

    public void SavePlayer(Player player)
    {
        // SQL Here
    }

    public Player LoadPlayer()
    {
        // SQL Here
    }
}