using System.Diagnostics;
using Newtonsoft.Json;
using RubiansPresale;
using Solnet.KeyStore;
using Solnet.Rpc;
using Solnet.Rpc.Builders;
using Solnet.Rpc.Models;
using Solnet.Rpc.Types;
using Solnet.Wallet;

namespace Presale.test;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public async Task TestMethod1()
    {
        var user = new SolanaKeyStoreService().RestoreKeystoreFromFile("/Users/bare/NFT/zenrepublic/testUser.json");
        var rpc = ClientFactory.GetClient("http://127.0.0.1:8899");


        var instruction = Lib.CreateCreateAllocationInstruction(100, new PublicKey("2iqp3bj3rKGJ9KT1U5rSkD57cKD4sWrXEAQ79oTVNvbW"), user.Account.PublicKey);

        var res = await PrepareAndSend(rpc, instruction, user.Account);
        Console.WriteLine(res.RawRpcResponse);

    }
    
    
    private async Task<Solnet.Rpc.Core.Http.RequestResult<string>> PrepareAndSend(IRpcClient rpc, TransactionInstruction instruction, Account signer){
        Console.WriteLine(rpc.GetRecentBlockHash(Commitment.Processed).RawRpcResponse);
        return await rpc.SendTransactionAsync(new TransactionBuilder().AddInstruction(instruction).SetFeePayer(signer.PublicKey).SetRecentBlockHash((await rpc.GetLatestBlockHashAsync(Commitment.Finalized)).Result.Value.Blockhash).Build(signer), false, Commitment.Processed);
    }
}