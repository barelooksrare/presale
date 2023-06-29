using System.Text;
using Presale.Accounts;
using Presale.Program;
using Solana.Unity.Programs;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Wallet;

namespace RubiansPresale
{
    public static class Lib
    {
        public struct KeyWithBump
        {
            public PublicKey Key { get; }
            public byte Bump { get; }

            public KeyWithBump(PublicKey key, byte bump)
            {
                Key = key;
                Bump = bump;
            }

            public static implicit operator PublicKey(KeyWithBump k) => k.Key;
        }

        private static PublicKey PROGRAM_ID = new PublicKey("41rsNvgfAqEqdMsSxegNghscr1xVoHi4aGZNjrpccG84");
        private static PublicKey TOKEN_METADATA_PROGRAM = new PublicKey("metaqbxxUerdq28cj1RbAWkYQm3ybzjb6a8bt518x1s");
        
        //Devnet
        //private static PublicKey PRESALE_TOKEN = new PublicKey("C2PQaR8QnS3C7peWVgAY4L2guSL1TYuT3qq4vQ88DjDf");
        //Mainnet
        // private static PublicKey PRESALE_TOKEN = new PublicKey("ESyHCUfKeT1ffLNRfCsjyHzNL4qN22kruVr8vYkPDR5r");
        //Localnet
        private static PublicKey PRESALE_TOKEN = new PublicKey("7fwA9aLTKmeueu9mJHU9QMBYEEfReMMMtkSoBBKXYwsH");

        public static KeyWithBump DerivePda(PublicKey programId, params object[] items)
        {
            List<byte[]> seeds = new List<byte[]>();
            foreach (var item in items)
            {
                if (item.GetType() == typeof(string)) seeds.Add(Encoding.UTF8.GetBytes((string)item));
                if (item.GetType() == typeof(PublicKey)) seeds.Add(((PublicKey)item).KeyBytes);
                if (item.GetType() == typeof(byte[])) seeds.Add((byte[])item);
            }
            PublicKey.TryFindProgramAddress(seeds, programId, out PublicKey key, out byte bump);
            return new KeyWithBump(key, bump);
        }

        
        private static KeyWithBump GetMetadataAccount(PublicKey mint)
        {
            return DerivePda(TOKEN_METADATA_PROGRAM, "metadata", TOKEN_METADATA_PROGRAM, mint);
        }

        private static KeyWithBump GetAllocationAccount(PublicKey exampleOrCollectionMint )
        {
            return DerivePda(PROGRAM_ID, exampleOrCollectionMint, PRESALE_TOKEN);
        }

        private static KeyWithBump GetPresaleTokenAccount(PublicKey exampleOrCollectionMint )
        {
            return DerivePda(PROGRAM_ID, "vault", GetAllocationAccount(exampleOrCollectionMint).Key);
        }

        public static TransactionInstruction CreateCreateAllocationInstruction(int allocationSize ,PublicKey collectionOrExampleMint, PublicKey admin)
        {
            var accounts = new CreateAllocationAccounts()
            {
                Signer = admin,
                SourceTokenAccount = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(admin, PRESALE_TOKEN),
                Allocation = GetAllocationAccount(collectionOrExampleMint),
                PresaleMint = PRESALE_TOKEN,
                PresaleTokenVault = GetPresaleTokenAccount(collectionOrExampleMint),
                AllocationRefMint = collectionOrExampleMint,
                RefMetadata = GetMetadataAccount(collectionOrExampleMint),
                TokenProgram = TokenProgram.ProgramIdKey,
                SystemProgram = SystemProgram.ProgramIdKey
            };

            return PresaleProgram.CreateAllocation(accounts, (ushort)allocationSize, PROGRAM_ID);
        }

        public static TransactionInstruction CreateCloseAllocationInstruction(PublicKey collectionOrExampleMint, PublicKey admin)
        {
            var accounts = new CloseAllocationAccounts()
            {
                Signer = admin,
                SignerTokenAccount = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(admin, PRESALE_TOKEN),
                Allocation = GetAllocationAccount(collectionOrExampleMint),
                PresaleMint = PRESALE_TOKEN,
                PresaleTokenVault = GetPresaleTokenAccount(collectionOrExampleMint),
                AllocationRefMint = collectionOrExampleMint,
                TokenProgram = TokenProgram.ProgramIdKey,
                AssociatedTokenProgram = AssociatedTokenAccountProgram.ProgramIdKey,
                SystemProgram = SystemProgram.ProgramIdKey
            };

            return PresaleProgram.CloseAllocation(accounts, PROGRAM_ID);
        }

        
        public static TransactionInstruction CreateCloseAllocationInstruction(Allocation allocation)
        {
            var accounts = new CloseAllocationAccounts()
            {
                Signer = allocation.AllocationAuthority,
                SignerTokenAccount = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(allocation.AllocationAuthority, PRESALE_TOKEN),
                Allocation = GetAllocationAccount(allocation.RefMint),
                PresaleMint = PRESALE_TOKEN,
                PresaleTokenVault = GetPresaleTokenAccount(allocation.RefMint),
                AllocationRefMint = allocation.RefMint,
                TokenProgram = TokenProgram.ProgramIdKey,
                AssociatedTokenProgram = AssociatedTokenAccountProgram.ProgramIdKey,
                SystemProgram = SystemProgram.ProgramIdKey
            };

            return PresaleProgram.CloseAllocation(accounts, PROGRAM_ID);
        }

        public static TransactionInstruction CreateBuyPresaleInstruction(Presale.Accounts.Allocation allocation, int amount, PublicKey user, PublicKey userNftMint, PublicKey userNftTokenAccount = null )
        {
            userNftTokenAccount = userNftTokenAccount ?? AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(user, userNftMint);
            var accounts = new ClaimAmountAccounts()
            {
                Signer = user,
                Allocation = GetAllocationAccount(allocation.RefMint),
                AllocationAuthority = allocation.AllocationAuthority,
                PresaleMint = allocation.PresaleToken,
                PresaleTokenVault = GetPresaleTokenAccount(allocation.RefMint),
                UserRecipientAccount = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(user, allocation.PresaleToken),
                UserNftMetadata = GetMetadataAccount(userNftMint),
                UserNftTokenAccount = userNftTokenAccount,
                TokenProgram = TokenProgram.ProgramIdKey,
                AssociatedTokenProgram = AssociatedTokenAccountProgram.ProgramIdKey,
                SystemProgram = SystemProgram.ProgramIdKey
            };
            return PresaleProgram.ClaimAmount(accounts, (ushort) amount, PROGRAM_ID);
        }

        public static async Task<List<Allocation>> FetchAllocations(IRpcClient rpc)
        {
            var list = new List<Solana.Unity.Rpc.Models.MemCmp>{new Solana.Unity.Rpc.Models.MemCmp{Bytes = Allocation.ACCOUNT_DISCRIMINATOR_B58, Offset = 0}};
            var res = await rpc.GetProgramAccountsAsync(PROGRAM_ID, Commitment.Confirmed, memCmpList: list);
            if (!res.WasSuccessful || !(res.Result?.Count > 0))
                return new List<Allocation>();
            List<Allocation> resultingAccounts = new List<Allocation>(res.Result.Count);
            resultingAccounts.AddRange(res.Result.Select(result => Allocation.Deserialize(Convert.FromBase64String(result.Account.Data[0]))));
            return resultingAccounts;
        }
    }
}
