using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System;

public static class SaveSystem
{
    private const string Key = "83dj43huSoLmsjru";

    public static void SaveLevelData(GameManagerSaveData manager, Options options)
    {
        
        var Result = LoadSaveDataFile();
        if (Result == null)
        {
            Debug.Log("Result is null");
            if (options == null)
            {
                options = new Options();
                options.UIScale = 1f;
            }
            CreateSaveFile(options);
            Result = LoadSaveDataFile();
        }

        List<LevelSaveData> LevelData = new List<LevelSaveData>();
        LevelData = Result.GetLevels;
        LevelSaveData PreviousLevelData = null;
        if (LevelData != null)
        {
            for (int i = 0; i < LevelData.Count; i++)
            {
                if (LevelData[i].GetLevelID == manager.LevelID)
                {
                    PreviousLevelData = LevelData[i];
                    LevelData.RemoveAt(i);
                    break;
                }
                else if (LevelData[i].GetLevelID == null)
                {
                    LevelData.RemoveAt(i);
                    break;
                }
            }

        }
        else
        {
            LevelData = new List<LevelSaveData>();
        }
        LevelSaveData CurrentLevelData = new LevelSaveData(manager, PreviousLevelData);
        List<LevelSaveData> NewLevelData = new List<LevelSaveData>();
        NewLevelData.Sort();
        NewLevelData = LevelData;
        Debug.Log("NewLevelData: " + NewLevelData);
        Debug.Log("CurrentLevelData: " + CurrentLevelData);

        NewLevelData.Add(CurrentLevelData);
        Debug.Log(CurrentLevelData);

        SaveFile newSaveFile = new SaveFile(LevelData, Result.GetPath, options);

        SaveDataFile(options, LoadSaveDataFile(), newSaveFile);



        //Make it so after the level has been saved, it checks for a file called SaveData.sdf, so it can be overwritten to have the new level's save data
    }

    public static SaveFile LoadSaveDataFile()
    {
        if (File.Exists(Application.dataPath+"/SaveData.sdf"))
        {
            byte[] Decrypted;
            using (var encrypted = new FileStream(Application.dataPath + "/SaveData.sdf", FileMode.Open))
            {
                using (var br = new BinaryReader(encrypted))
                {
                    br.BaseStream.Position = 0;
                    byte[] Result = br.ReadBytes(50000000);
                    Decrypted = XTEA.Decrypt(Result, Key);
                }
                
                
            }
           

            BinaryFormatter binaryFormatter = new BinaryFormatter();
            SaveFile LoadedSaveFile;
            using (var memoryStream = new MemoryStream())
            {
                using (var Writer = new BinaryWriter(memoryStream))
                {
                    Writer.Write(Decrypted);
                    memoryStream.Position = 0;
                    LoadedSaveFile = binaryFormatter.Deserialize(memoryStream) as SaveFile;
                }
            }
            
            return LoadedSaveFile;
        }
        else
            return null;
    }

    public static void SaveDataFile(Options options = null, SaveFile CurrentSaveFile = null, SaveFile NewSaveFile = null)
    {
        //If The Current save file is null, then that means it's the first time saving, so create a new file
        if (CurrentSaveFile == null)
        {
            Debug.Log("Save File is being created");
            GameObjectFinder.GetSaveFile = CreateSaveFile(options);
        }
        else if (NewSaveFile != null)
        {
            Debug.Log("Save File is being overwritten");
            GameObjectFinder.GetSaveFile = OverwriteSaveFile(NewSaveFile);
        }
        else if (CurrentSaveFile != null && NewSaveFile == null && options != null)
        {
            Debug.Log("Save File's Options are being overwritten");
            SaveFile NewOptionsSave = new SaveFile(CurrentSaveFile.GetLevels, CurrentSaveFile.GetPath, options);
            GameObjectFinder.GetSaveFile = OverwriteSaveFile(NewOptionsSave);
        }
        //If the save file isnt null, then that means you have to overwrite an existing file, so you use the path to find it to overwrite it
    }

    private static SaveFile CreateSaveFile(Options options)
    {
        BinaryFormatter formatter = new BinaryFormatter();
        var Path = Application.dataPath + "/SaveData.sdf";
        MemoryStream stream = new MemoryStream();
        SaveFile CurrentSaveFile = new SaveFile(null, Path, options);
        formatter.Serialize(stream, CurrentSaveFile);
        byte[] EncryptedSaveFile = XTEA.Encrypt(stream.ToArray(), Key);
        stream.Close();

        FileStream WriteStream = new FileStream(Path, FileMode.Create);
        BinaryWriter encBW = new BinaryWriter(WriteStream);
        encBW.Write(EncryptedSaveFile);

        WriteStream.Close();

        Debug.Log("Save Created");

        return CurrentSaveFile;
    }

    

    private static SaveFile OverwriteSaveFile(SaveFile NewSaveFile)
    {

        bool FileFound = false;
        do
        {
            var CurrentSaveFile = LoadSaveDataFile();
            if (CurrentSaveFile == null)
            {
                CreateSaveFile(NewSaveFile.GetOptions);
            }
            else
            {
                FileFound = true;
            }
        } while (!FileFound);

        if (NewSaveFile == null)
        {
            return null;
        }

       
        BinaryFormatter formatter = new BinaryFormatter();
        var Path = Application.dataPath + "/SaveData.sdf";
        MemoryStream stream = new MemoryStream();
        formatter.Serialize(stream, NewSaveFile);
        byte[] EncryptedSaveFile = XTEA.Encrypt(stream.ToArray(), Key);
        stream.Close();

        FileStream WriteStream = new FileStream(Path, FileMode.Create);
        BinaryWriter encBW = new BinaryWriter(WriteStream);
        encBW.Write(EncryptedSaveFile);

        WriteStream.Close();

        Debug.Log("Save Overwritten");
        return NewSaveFile;
    }

    public static LevelSaveData LoadLevelData(string LevelID)
    {
        var Result = LoadSaveDataFile();
        List<LevelSaveData> LevelList = Result.GetLevels;
        if (LevelList == null)
        {
            return null;
        }
        else
        {
            for (int i = 0; i < LevelList.Count; i++)
            {
                if (LevelList[i].GetLevelID == LevelID)
                {
                    return LevelList[i];
                }
            }
            return null;
        }

    }

}

public static class XTEA
{
    //The XTEA algorithm runs for 64 rounds, but since a single cycle counts as 2 rounds, 64/2 = 32 cycles
    private const uint Rounds = 32;

    public static byte[] Encrypt(byte[] Data, string Key)
    {
        byte[] KeyBytes = Encoding.Unicode.GetBytes(Key);
        //The key is hashed here
        uint[] KeyInts = CreateKey(KeyBytes);
        //This datablock variable stores 2 32-bit unsigned integers for when the data is encoded
        uint[] DataBlock = new uint[2];
        byte[] Result = new byte[NextMultipleOf8(Data.Length + 4)];

        byte[] LengthBuffer = BitConverter.GetBytes(Data.Length);
        Array.Copy(LengthBuffer, Result, LengthBuffer.Length);
        Array.Copy(Data, 0, Result, LengthBuffer.Length, Data.Length);
        using (MemoryStream MemStream = new MemoryStream(Result))
        {
            using (BinaryWriter BinWriter = new BinaryWriter(MemStream))
            {
                //i+=8 because the data is processed in 64 bit sections
                for (int i = 0; i < Result.Length; i += 8)
                {
                    //The 64 bit buffer is split into two
                    DataBlock[0] = BitConverter.ToUInt32(Result, i);
                    DataBlock[1] = BitConverter.ToUInt32(Result, i + 4);
                    Encode(Rounds, DataBlock, KeyInts);
                    BinWriter.Write(DataBlock[0]);
                    BinWriter.Write(DataBlock[1]);
                }
            }
        }
        return Result;
    }

    private static void Encode(uint rounds, uint[] v, uint[] key)
    {
        uint v0 = v[0];
        uint v1 = v[1];
        uint sum = 0;
        uint delta = 0x9E3779B9;
        for (uint i = 0; i < rounds; i++)
        {
            //XOR gates used on the bit-shifted values of v1 and (sum+key)
            v0 += (((v1 << 4) ^ (v1 >> 5)) + v1) ^ (sum + key[sum & 3]);
            //sum = sum + delta for each round
            sum += delta;
            //XOR gates used on bit-shifted values of v0 and (sum+key)
            v1 += (((v0 << 4) ^ (v0 >> 5)) + v0) ^ (sum + key[(sum >> 11) & 3]);
        }
        v[0] = v0;
        v[1] = v1;
    }

    public static byte[] Decrypt(byte[] Data, string Key)
    {
        byte[] KeyBytes = Encoding.Unicode.GetBytes(Key);

        if (Data.Length % 8 != 0)
        {
            throw new ArgumentException("Encrypted Data length must be a multiple of 8 bytes (64bit)");
        }
        uint[] KeyBuffer = CreateKey(KeyBytes);
        uint[] DataBlock = new uint[2];
        byte[] Buffer = new byte[Data.Length];
        Array.Copy(Data, Buffer, Data.Length);
        using (MemoryStream MemStream = new MemoryStream(Buffer))
        {
            using (BinaryWriter BinWriter = new BinaryWriter(MemStream))
            {
                for (int i = 0; i < Buffer.Length; i += 8)
                {
                    DataBlock[0] = BitConverter.ToUInt32(Buffer, i);
                    DataBlock[1] = BitConverter.ToUInt32(Buffer, i + 4);
                    Decode(Rounds, DataBlock, KeyBuffer);
                    BinWriter.Write(DataBlock[0]);
                    BinWriter.Write(DataBlock[1]);
                }
            }
        }

        uint Length = BitConverter.ToUInt32(Buffer, 0);
        if (Length > Buffer.Length - 4)
        {
            throw new ArgumentException("Invalid Encrypted Data");
        }
        byte[] Result = new byte[Length];
        Array.Copy(Buffer, 4, Result, 0, Length);
        return Result;
    }

    private static void Decode(uint rounds, uint[] v, uint[] key)
    {
        uint v0 = v[0];
        uint v1 = v[1];
        uint delta = 0x9E3779B9;
        uint sum = delta * rounds;
        for (uint i = 0; i < rounds; i++)
        {
            v1 -= (((v0 << 4) ^ (v0 >> 5)) + v0) ^ (sum + key[(sum >> 11) & 3]);
            sum -= delta;
            v0 -= (((v1 << 4) ^ (v1 >> 5)) + v1) ^ (sum + key[sum & 3]);
        }
        v[0] = v0;
        v[1] = v1;
    }






    private static uint[] CreateKey(byte[] Key)
    {
        //The key is 128 bit, so 16 bytes
        byte[] Hash = new byte[16];
        for (int i = 0; i < Key.Length; i++)
        {
            //The hash's values are set by having the key perform a XOR operation on each value in hash over and over until the Key reaches the end.
            Hash[i % 16] = (byte)((31 * Hash[i % 16]) ^ Key[i]);
        }
        for (int i = Key.Length; i < Hash.Length; i++)
        {
            
            Hash[i] = (byte)(17 * i ^ Key[i % Key.Length]);
        }
        return new[] {
            BitConverter.ToUInt32(Hash, 0), BitConverter.ToUInt32(Hash, 4),
            BitConverter.ToUInt32(Hash, 8), BitConverter.ToUInt32(Hash, 12)
        };
    }

    private static int NextMultipleOf8(int Length)
    {
        //This will return the index of the Next Multiple of 8 
        return (Length + 7) / 8 * 8;
    }


}

public class GameManagerSaveData
{
    public string LevelID;
    public List<string> Seed;
    public bool IsFinished;
    public int LevelLength;
    public int Points;
    public int TimeSpent;

    public GameManagerSaveData(string _LevelID, List<string> _Seed, bool _IsFinished, int _LevelLength, int _Points, int _TimeSpent)
    {
        LevelID = _LevelID;
        Seed = _Seed;
        IsFinished = _IsFinished;
        LevelLength = _LevelLength;
        Points = _Points;
        TimeSpent = _TimeSpent;
        Debug.Log(Seed[1]);
    }
}