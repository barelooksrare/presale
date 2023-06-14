using System.Text;
using Presale.Program;
using Solnet.Programs;
using Solnet.Rpc;
using Solnet.Rpc.Models;
using Solnet.Wallet;

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

        private static PublicKey PROGRAM_ID = new PublicKey("ETTfbsihcY4rcAWdo4G7pATGNfr744LPv4ziheRz5EPu");
        private static PublicKey TOKEN_METADATA_PROGRAM = new PublicKey("metaqbxxUerdq28cj1RbAWkYQm3ybzjb6a8bt518x1s");
        
        //Devnet
        private static PublicKey PRESALE_TOKEN = new PublicKey("C2PQaR8QnS3C7peWVgAY4L2guSL1TYuT3qq4vQ88DjDf");
        //Mainnet

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
            return DerivePda(PROGRAM_ID, "vault", GetAllocationAccount(exampleOrCollectionMint));
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
    }
}
