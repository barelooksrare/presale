using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Solana.Unity;
using Solana.Unity.Programs.Abstract;
using Solana.Unity.Programs.Utilities;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Builders;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Core.Sockets;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Wallet;
using Presale;
using Presale.Program;
using Presale.Errors;
using Presale.Accounts;

namespace Presale
{
    namespace Accounts
    {
        public partial class Allocation
        {
            public static ulong ACCOUNT_DISCRIMINATOR => 12719037929104841363UL;
            public static ReadOnlySpan<byte> ACCOUNT_DISCRIMINATOR_BYTES => new byte[]{147, 154, 3, 177, 155, 25, 131, 176};
            public static string ACCOUNT_DISCRIMINATOR_B58 => "RgvPtNxzZXm";
            public PublicKey AllocationAuthority { get; set; }

            public bool IsActive { get; set; }

            public bool AllocationIsCreator { get; set; }

            public PublicKey CreatorOrCollection { get; set; }

            public PublicKey RefMint { get; set; }

            public PublicKey PresaleToken { get; set; }

            public ushort AmountAllocated { get; set; }

            public ushort AmountSpent { get; set; }

            public static Allocation Deserialize(ReadOnlySpan<byte> _data)
            {
                int offset = 0;
                ulong accountHashValue = _data.GetU64(offset);
                offset += 8;
                if (accountHashValue != ACCOUNT_DISCRIMINATOR)
                {
                    return null;
                }

                Allocation result = new Allocation();
                result.AllocationAuthority = _data.GetPubKey(offset);
                offset += 32;
                result.IsActive = _data.GetBool(offset);
                offset += 1;
                result.AllocationIsCreator = _data.GetBool(offset);
                offset += 1;
                result.CreatorOrCollection = _data.GetPubKey(offset);
                offset += 32;
                result.RefMint = _data.GetPubKey(offset);
                offset += 32;
                result.PresaleToken = _data.GetPubKey(offset);
                offset += 32;
                result.AmountAllocated = _data.GetU16(offset);
                offset += 2;
                result.AmountSpent = _data.GetU16(offset);
                offset += 2;
                return result;
            }
        }
    }

    namespace Errors
    {
        public enum PresaleErrorKind : uint
        {
        }
    }

    public partial class PresaleClient : TransactionalBaseClient<PresaleErrorKind>
    {
        public PresaleClient(IRpcClient rpcClient, IStreamingRpcClient streamingRpcClient, PublicKey programId) : base(rpcClient, streamingRpcClient, programId)
        {
        }

        public async Task<Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<Allocation>>> GetAllocationsAsync(string programAddress, Commitment commitment = Commitment.Confirmed)
        {
            var list = new List<Solana.Unity.Rpc.Models.MemCmp>{new Solana.Unity.Rpc.Models.MemCmp{Bytes = Allocation.ACCOUNT_DISCRIMINATOR_B58, Offset = 0}};
            var res = await RpcClient.GetProgramAccountsAsync(programAddress, commitment, memCmpList: list);
            if (!res.WasSuccessful || !(res.Result?.Count > 0))
                return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<Allocation>>(res);
            List<Allocation> resultingAccounts = new List<Allocation>(res.Result.Count);
            resultingAccounts.AddRange(res.Result.Select(result => Allocation.Deserialize(Convert.FromBase64String(result.Account.Data[0]))));
            return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<Allocation>>(res, resultingAccounts);
        }

        public async Task<Solana.Unity.Programs.Models.AccountResultWrapper<Allocation>> GetAllocationAsync(string accountAddress, Commitment commitment = Commitment.Confirmed)
        {
            var res = await RpcClient.GetAccountInfoAsync(accountAddress, commitment);
            if (!res.WasSuccessful)
                return new Solana.Unity.Programs.Models.AccountResultWrapper<Allocation>(res);
            var resultingAccount = Allocation.Deserialize(Convert.FromBase64String(res.Result.Value.Data[0]));
            return new Solana.Unity.Programs.Models.AccountResultWrapper<Allocation>(res, resultingAccount);
        }

        public async Task<SubscriptionState> SubscribeAllocationAsync(string accountAddress, Action<SubscriptionState, Solana.Unity.Rpc.Messages.ResponseValue<Solana.Unity.Rpc.Models.AccountInfo>, Allocation> callback, Commitment commitment = Commitment.Confirmed)
        {
            SubscriptionState res = await StreamingRpcClient.SubscribeAccountInfoAsync(accountAddress, (s, e) =>
            {
                Allocation parsingResult = null;
                if (e.Value?.Data?.Count > 0)
                    parsingResult = Allocation.Deserialize(Convert.FromBase64String(e.Value.Data[0]));
                callback(s, e, parsingResult);
            }, commitment);
            return res;
        }

        public async Task<RequestResult<string>> SendCreateAllocationAsync(CreateAllocationAccounts accounts, ushort allocationAmount, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.PresaleProgram.CreateAllocation(accounts, allocationAmount, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendClaimAmountAsync(ClaimAmountAccounts accounts, ushort amount, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.PresaleProgram.ClaimAmount(accounts, amount, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        protected override Dictionary<uint, ProgramError<PresaleErrorKind>> BuildErrorsDictionary()
        {
            return new Dictionary<uint, ProgramError<PresaleErrorKind>>{};
        }
    }

    namespace Program
    {
        public class CreateAllocationAccounts
        {
            public PublicKey Signer { get; set; }

            public PublicKey SourceTokenAccount { get; set; }

            public PublicKey Allocation { get; set; }

            public PublicKey PresaleMint { get; set; }

            public PublicKey PresaleTokenVault { get; set; }

            public PublicKey AllocationRefMint { get; set; }

            public PublicKey RefMetadata { get; set; }

            public PublicKey TokenProgram { get; set; }

            public PublicKey SystemProgram { get; set; }
        }

        public class ClaimAmountAccounts
        {
            public PublicKey Signer { get; set; }

            public PublicKey Allocation { get; set; }

            public PublicKey AllocationAuthority { get; set; }

            public PublicKey PresaleMint { get; set; }

            public PublicKey PresaleTokenVault { get; set; }

            public PublicKey UserRecipientAccount { get; set; }

            public PublicKey UserNftMetadata { get; set; }

            public PublicKey UserNftTokenAccount { get; set; }

            public PublicKey TokenProgram { get; set; }

            public PublicKey AssociatedTokenProgram { get; set; }

            public PublicKey SystemProgram { get; set; }
        }

        public static class PresaleProgram
        {
            public static Solana.Unity.Rpc.Models.TransactionInstruction CreateAllocation(CreateAllocationAccounts accounts, ushort allocationAmount, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Signer, true), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.SourceTokenAccount, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Allocation, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.PresaleMint, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.PresaleTokenVault, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.AllocationRefMint, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.RefMetadata, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.TokenProgram, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(10204488941721995618UL, offset);
                offset += 8;
                _data.WriteU16(allocationAmount, offset);
                offset += 2;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction ClaimAmount(ClaimAmountAccounts accounts, ushort amount, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Signer, true), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Allocation, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.AllocationAuthority, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.PresaleMint, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.PresaleTokenVault, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.UserRecipientAccount, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.UserNftMetadata, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.UserNftTokenAccount, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.TokenProgram, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.AssociatedTokenProgram, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(7638308992207116314UL, offset);
                offset += 8;
                _data.WriteU16(amount, offset);
                offset += 2;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }
        }
    }
}