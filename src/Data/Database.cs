# this is the file for handling the Database for the project
# Handling OpenConnection(), CloseConnection(), ExecuteQuery(), ExecuteNonQuery()

import SQLite

Public class Database{
    private readonly string _connectionString;

    public GameRepository(Database database){
        _dataabase = database;
    }

    public void SavePlayer(Player player){
        // SQL content goes here
    }
    public Player LoadPlayer(){
        //SQL content goes here
    }
}

OpenConnection() 

CloseConnection(), 

ExecuteQuery(), 

ExecuteNonQuery()