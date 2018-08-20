using System;
using System.Text;

#if NEOSC
using Neunity.Adapters.NEO;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using Helper = Neo.SmartContract.Framework.Helper;
#else
using Neunity.Adapters.Unity;
#endif

using System.Numerics;
using Neunity.Tools;

namespace Asymmentropy
{
    public class Lottery : SmartContract
    {

        public const string sp = "/";
        public const byte unknown = 0;
        public const byte known = 1;
        public static readonly byte[] Owner = "AK2nJJpJr6o664CWJKi1QRXjqeic2zRp8y".ToScriptHash();

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
        public class Entry{
            public BigInteger gameId;
            public byte[] address;
            public byte[] pick;
            public byte[] hidden;
            public byte[] hiddenHash;
        }


        [Serializable]
        public class Game{
            public BigInteger heightStage1;
            public BigInteger heightStage2;
            public BigInteger numEntries;
            public bool isFinalized;
            public byte[] winnerPick;
        }

        /**
            Deploy or reset the game world. Only the owner account can do it. 
        */
        private static bool Deploy(){
            BigInteger i = 0;
            NuIO.SetStorageWithKey(Global.keyNumGames, Op.BigInt2Bytes(i));
            return true;
        }

        /**
            Start a new game. Only the owner account can do it. 
        */
        private static bool StartGame(){
            BigInteger num = Op.Bytes2BigInt(NuIO.GetStorageWithKey(Global.keyNumGames));

            Game game = new Game();
            BigInteger currentHeight = Blockchain.GetHeight();
            game.heightStage1 = currentHeight + Global.Stage1Height;
            game.heightStage2 = currentHeight + Global.Stage2Height;
            game.numEntries = 0;
            game.isFinalized = false;
            byte[] data = game.Serialize();
            BigInteger gameid = NumGames();
            NuIO.SetStorageWithKeyPath(data, Global.keyGame, Op.BigInt2String(gameid));
            NuIO.SetStorageWithKey(Global.keyNumGames, Op.BigInt2Bytes(gameid + 1));

            return true;

        }


        /**
            In the 1st stage of each game, players are only allowed to submit the 
            hidden number's hash, rather than it hidden value itself.
        */
        public static byte[] PutEntry(BigInteger gameId, byte[] address, byte[] pick, byte[] hiddenHash)
        {

            Game game = GetGame(gameId);
            if(game == null){
                return NuTP.RespDataWithCode(NuTP.SysDom, NuTP.Code.BadRequest);
            }
            else
            {
                
                if (Blockchain.GetHeight() >= game.heightStage1)
                {
                    return NuTP.RespDataWithCode(NuTP.SysDom, NuTP.Code.Forbidden);;
                }
                else
                {
                    Entry entry = new Entry();
                    entry.gameId = gameId;
                    entry.address = address;
                    entry.pick = pick;
                    entry.hiddenHash = hiddenHash;
                    byte[] data = entry.Serialize();
                    BigInteger id = NumEntries(gameId);
                    NuIO.SetStorageWithKeyPath(data, Global.keyGame, Op.BigInt2String(gameId),Global.keyEntry, Op.BigInt2String(id));

                    game.numEntries += 1;
                    NuIO.SetStorageWithKeyPath(game.Serialize(), Global.keyGame, Op.BigInt2String(gameId));

                    return NuTP.RespDataSucWithBody(data);
                }

            }

        }


        /**
            In the 2nd stage of each game, players submit the hidden number(prove), nobody can fake it 
            since it must match the hash every player submitted in during first round.
            If a user failed to submit the prove, s/he will be elimiated from this game.
        */
        public static byte[] PutProve(BigInteger gameId ,BigInteger entryId, byte[] hidden)
        {
            Entry entry = GetEntry(gameId,entryId);

            Game game = GetGame(gameId);
            BigInteger height = Blockchain.GetHeight();
            if (height < game.heightStage1 || height > game.heightStage2)
            {
                return NuTP.RespDataWithCode(NuTP.SysDom,NuTP.Code.Forbidden);
            }
            else
            {   
                if (Hash256(hidden) == entry.hiddenHash)
                {
                    entry.hidden = hidden;
                    return NuTP.RespDataSuccess();
                }
                else
                {
                    return NuTP.RespDataWithCode(NuTP.SysDom, NuTP.Code.BadRequest);
                }
            }

        }

        /**
            After 2nd stage finished, anybody can query the winnerPick.
        */
        public static byte[] CalcResult(BigInteger gameId){
            Game game = GetGame(gameId);
            if(game == null){
                return NuTP.RespDataWithCode(NuTP.SysDom, NuTP.Code.BadRequest);
            }
            else{
                if (game.winnerPick[0]==1)
                {
                    return NuTP.RespDataSucWithBody(game.winnerPick);
                }
                else
                {
                    BigInteger height = Blockchain.GetHeight();
                    if (height < game.heightStage2)
                    {
                        return NuTP.RespDataWithCode(NuTP.SysDom, NuTP.Code.Forbidden);
                    }
                    else
                    {
                        BigInteger salt = 0;
                        for (int i = 0; i < game.numEntries; i++)
                        {
                            Entry entry = GetEntry(gameId, i);
                            if (entry.hidden.Length != 0)
                            {
                                salt += Op.Bytes2BigInt(entry.hidden);
                            }
                        }
                        byte[] winnerPick = Op.SubBytes(Hash256(salt.ToByteArray()), 0, 1);
                        game.winnerPick = winnerPick;
                        return NuTP.RespDataSucWithBody(winnerPick);
                    }
                }
            }
        }

        public static byte[] IsWinner(BigInteger gameId, BigInteger entryId){
            Game game = GetGame(gameId);
            if (game == null){
                return NuTP.RespDataWithCode(NuTP.SysDom, NuTP.Code.BadRequest);
            }
            else{
                BigInteger height = Blockchain.GetHeight();
                if (height < game.heightStage2){
                    return NuTP.RespDataWithCode(NuTP.SysDom, NuTP.Code.Forbidden); 
                }
                else{
                    
                    Entry entry = GetEntry(gameId, entryId);
                    bool ret = entry.pick == game.winnerPick;
                    return NuTP.RespDataSucWithBody(Op.Bool2Bytes(ret));
                }

            }
        }

        public static object Main(string op, params object[] args)
        {
            if (Runtime.Trigger == TriggerType.Verification)
            {
                
            }

            if (op == "deploy")
            {
                if (Runtime.CheckWitness(Owner))
                {
                    return Deploy();
                }

            }

            if(op == "startGame")
            {
                if (Runtime.CheckWitness(Owner))
                {
                    return StartGame();
                }
            }

            if(op == "putEntry")
            {
                BigInteger gameID = ((byte[])args[0]).AsBigInteger();
                byte[] address = (byte[])args[1];
                byte[] pick = (byte[])args[2];
                byte[] hiddenHash = (byte[])args[3];
                return PutEntry(gameID, address, pick, hiddenHash);
            }

            if(op == "putProve")
            {
                BigInteger gameId = (BigInteger)args[0];
                BigInteger entryId = (BigInteger)args[1];
                byte[] hidden = (byte[])args[2];
                return PutProve(gameId, entryId, hidden);
            }


            if (op == "checkResult")
            {
                BigInteger gameId = ((byte[])args[0]).AsBigInteger();
                return CalcResult(gameId);
            }
            else return false;
        }
    
        public static BigInteger NumGames()
        {
            return Op.Bytes2BigInt(NuIO.GetStorageWithKey(Global.keyNumGames));
        }

        public static Game GetGame(BigInteger gameId)
        {
            return (Game)NuIO.GetStorageWithKeyPath(Global.keyGame, Op.BigInt2String(gameId)).Deserialize();
        }

        public static BigInteger NumEntries(BigInteger gameId)
        {
            Game game = GetGame(gameId);
            return game.numEntries;

        }

        public static Entry GetEntry(BigInteger gameId, BigInteger entryId)
        {

            return (Entry)NuIO.GetStorageWithKeyPath(Global.keyGame, Op.BigInt2String(gameId), Global.keyEntry, Op.BigInt2String(entryId)).Deserialize();
        }
    }
}
