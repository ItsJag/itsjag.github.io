using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;


[Serializable]
public class LevelSaveData
{
    private string LevelID;
    private int HighScore;
    private List<string> Seed;
    private bool IsFinished;
    private List<LevelScore> LevelData;
    private LevelScore CurrentLevelData;
    private int LevelLength;

    //This class will be constructed upon a player completing a level, so that means the GameManager can be used to keep track of all the stats within the level
    public LevelSaveData(GameManagerSaveData manager, LevelSaveData PreviousLevelData = null)
    {
        IsFinished = manager.IsFinished;
        if (IsFinished)
        {
            CurrentLevelData = new LevelScore(manager, this);
        }
        else
        {
            CurrentLevelData = new LevelScore(0,0,this);
        }
        //Debug.Log(manager);
        //Debug.Log(manager.Seed[1]);
        Seed = manager.Seed;
        LevelLength = manager.LevelLength;
        //In the case this isn't the first time the player has played this level... 
        if (PreviousLevelData != null)
        {

            if (PreviousLevelData.GetHighScore > CurrentLevelData.EndScore)
            {
                HighScore = PreviousLevelData.GetHighScore;
            }
            else
            {
                HighScore = CurrentLevelData.EndScore;
            }

            //This section is new, its part of the new LevelScore class integration + Leaderboards

            LevelData = new List<LevelScore>();
            LevelData = PreviousLevelData.GetLevelData;
            LevelData.Add(CurrentLevelData);


        }
        else 
        {
            
            HighScore = CurrentLevelData.EndScore;
            LevelData = new List<LevelScore>();
            LevelData.Add(CurrentLevelData);
        }
        
        LevelID = manager.LevelID;


    }

    public int GetEndScore
    {
        get
        {
            return CurrentLevelData.EndScore;
        }
    }

    public string GetLevelID
    {
        get
        {
            return LevelID;
        }
    }

    public int GetHighScore
    {
        get
        {
            return HighScore;
        }
    }

    public int GetTimePlayed
    {
        get
        {
            return CurrentLevelData.TimePlayed;
        }
    }

    public List<string> GetSeed
    {
        get
        {
            return Seed;
        }
    }

    public bool GetIsFinished
    {
        get
        {
            return IsFinished;
        }
    }

    public List<LevelScore> GetLevelData
    {
        get
        {
            return LevelData;
        }
    }

    public int GetLevelLength
    {
        get
        {
            return LevelLength;
        }
    }

}

[Serializable]
public class SaveFile
{
    private List<LevelSaveData> Levels;
    private Options options;
    string Path;
    //Make it check whether the saveID in a LevelSaveData is equal to the assigned SaveFileID

    public SaveFile(List<LevelSaveData> _levels, string _path, Options _options)
    {
        Levels = _levels;
        Path = _path;
        if (_options == null)
        {
            options = new Options();
        }
        else
        {
            options = _options;
        }
    }

    public List<LevelSaveData> GetLevels
    {
        get
        {
            return Levels;
        }
        
    }

    public string GetPath
    {
        get
        {
            return Path;
        }
    }

    public Options GetOptions
    {
        get
        {
            return options;
        }
    }

    public List<Bind> GetBinds
    {
        get
        {
            return options.KeyBinds;
        }
    }

    public int GetDifficulty
    {
        get
        {
            return options.Difficulty;
        }
    }

    public float GetCameraScale
    {
        get
        {
            return options.CameraScale;
        }
    }

    public float GetUIScale
    {
        get
        {
            return options.UIScale;
        }
    }

    public bool GetSprintToggle
    {
        get
        {
            return options.SprintToggle;
        }
    }

}
[Serializable]
public class LevelScore
{
    public int EndScore;
    public int TimePlayed;
    public DateTime FinishDate;
    public LevelSaveData ParentLevel;

    public LevelScore(int _endscore, int _timeplayed, LevelSaveData _parent)
    {
        EndScore = _endscore;
        TimePlayed = _timeplayed;
        FinishDate = DateTime.Now;
        ParentLevel = _parent;
    }

    public LevelScore(GameManagerSaveData manager, LevelSaveData _parent)
    {
        EndScore = manager.Points;
        TimePlayed = manager.TimeSpent;
        FinishDate = DateTime.Now;
        ParentLevel = _parent;
    }
        
}

