using System;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Helper = Neo.SmartContract.Framework.Helper;
using System.Text;

using System.ComponentModel;
using System.Numerics;
using System.Collections.Generic;


namespace Neo.SmartContract
{
    public class HelloWorld : Framework.SmartContract
    {

	    public const string sp = "/";
		public const byte unknown = 0;
		public const byte known = 1;


		public static class Global{
			public const string keyNumEntries = "numEntries";
			public const string keyNumGames = "numGames";
			public const string keyEntry = "e";
			public const string keyGame = "g";
			public const long MaxEntries = 999999;
			public const long PerEntryGas = 1;
			public const long Stage1Height = 3000;
			public const long Stage2Height = 6000;
		}

		[Serializable]
		public class Var{
			public BigInteger numEntries = 0;
		}

		[Serializable]
		public class Entry{
			public BigInteger gameId;
			public byte[] address;
			public byte pick;
			public byte[] hidden;
			public byte[] hiddenHash;
		}


		[Serializable]
		public class Game{
			public BigInteger heightStage1;
			public BigInteger heightStage2;
			public byte[] winnerPick;
		}
        
		public static BigInteger numAllEntries(){
			return Storage.Get(Storage.CurrentContext, Global.keyNumEntries).AsBigInteger();
		}

		public static BigInteger numAllGames()
        {
			return Storage.Get(Storage.CurrentContext, Global.keyNumGames).AsBigInteger();
        }
        
		public static Entry[] getAllEntries(BigInteger gameId){
			//to be decided
			return new Entry[12];
		}

		private static Game getGame(BigInteger gameId){
			if( numAllGames()>= gameId)
			{
				return null;
			}
			else
			{
				byte[] data = Storage.Get(Storage.CurrentContext, Global.keyGame + sp + gameId.ToString());
				return (Game)data.Deserialize();
			}

		}
        
		public static bool deploy(){
			Storage.Put(Storage.CurrentContext,Global.keyNumEntries, 0.ToString());
			return true;
		}
        
        
		public static bool startGame(){
			BigInteger num = Storage.Get(Storage.CurrentContext, Global.keyNumGames).AsBigInteger();
			Game game = new Game();
			BigInteger currentHeight = Blockchain.GetHeight();
			game.heightStage1 = currentHeight + Global.Stage1Height;
			game.heightStage2 = currentHeight + Global.Stage2Height;
			game.winnerPick = new byte[2]{ unknown,0};
			byte[] data = game.Serialize();
			BigInteger id = numAllGames();
			Storage.Put(Storage.CurrentContext, Global.keyGame + sp + id.ToString(),data);

			return true;


		}

		private static byte[] hash(byte[] data){
			//to be decided
			return data;
		}

		private static byte calcResult(BigInteger gameId){
			Entry[] entries = getAllEntries(gameId);
			BigInteger ret = 0;
			for (int i = 0; i < entries.Length;i++)
			{
				ret = ret + entries[i].hidden.AsBigInteger();

			}
			return hash(ret.ToByteArray())[0];
		}
        

		public static BigInteger putEntry(BigInteger gameId,byte[]address, byte pick, byte[] hiddenHash){
			if (gameId >= numAllGames()) return -1;
			else
			{
				Game game = getGame(gameId);
				if(Blockchain.GetHeight()>= game.heightStage1)
				{
					return -1;
				}
				else
				{
					Entry entry = new Entry();
                    entry.gameId = gameId;
                    entry.address = address;
                    entry.pick = pick;
                    entry.hiddenHash = hiddenHash;
                    byte[] data = entry.Serialize();
                    BigInteger id = numAllEntries();
                    Storage.Put(Storage.CurrentContext, Global.keyEntry + sp + id.ToString(), data);
                    return id;
                }

			}
            
		}

		public static Entry getEntry(BigInteger entryId){
			byte[] data = Storage.Get(Storage.CurrentContext, Global.keyEntry + sp + entryId.ToString());
			if (data.Length > 0)
			{
				return (Entry)data.Deserialize();
			}
			else
			{
				return null;
			}
		}

		public static bool putProve(BigInteger entryId, byte[]hidden){
			Entry entry = getEntry(entryId);
			BigInteger gameId = entry.gameId;
			Game game = getGame(gameId);
			BigInteger height = Blockchain.GetHeight();
			if(height< game.heightStage1 || height>game.heightStage2)
			{
				return false;
			}
			else
			{

				if (hash(hidden) == entry.hiddenHash)
                {
                    entry.hidden = hidden;
                    return true;
                }
                else
                {
                    return false;
                }
			}

		}

        

		public static byte[] checkResult(BigInteger gameId){
			BigInteger height = Blockchain.GetHeight();
			Game game = getGame(gameId);
			if(height<= game.heightStage2)
			{
				return new byte[2]{unknown,0};
			}
			else if (game.winnerPick[0] == known){
				return game.winnerPick;
			}
			else
			{
				byte result = calcResult(gameId);
				game.winnerPick = new byte[2]{known,result};
				return game.winnerPick;
			}
		}

		 

		public static object Main(string op, params object[] args)
        {
			if(op == "deploy")
			{
				return deploy();
			}
			if(op == "startGame")
			{
				return startGame();
			}
			if(op == "putEntry")
			{
				BigInteger id = ((byte[])args[0]).AsBigInteger();
				byte[] address = (byte[])args[1];
				byte pick = ((byte[])args[2])[0];
				byte[] hiddenHash = (byte[])args[3];
				return putEntry(id, address, pick, hiddenHash);
			}

			if(op == "putProve")
			{
				BigInteger id = ((byte[])args[0]).AsBigInteger();
				byte[] hidden = (byte[])args[1];
				return putProve(id, hidden);
			}
			if (op == "checkResult")
			{
				BigInteger id = ((byte[])args[0]).AsBigInteger();
				return checkResult(id);
			}
			else return false;
        }
    }
}
