import * as anchor from "@coral-xyz/anchor";
import { Program } from "@coral-xyz/anchor";
import { Presale } from "../target/types/presale";
import { ASSOCIATED_PROGRAM_ID, TOKEN_PROGRAM_ID } from "@coral-xyz/anchor/dist/cjs/utils/token";

describe("presale", () => {
  // Configure the client to use the local cluster.
  anchor.setProvider(anchor.AnchorProvider.env());
  const connection = anchor.getProvider().connection;
  const program = anchor.workspace.Presale as Program<Presale>;
  const metadataProgram = new anchor.web3.PublicKey("metaqbxxUerdq28cj1RbAWkYQm3ybzjb6a8bt518x1s");
  const user = anchor.getProvider().publicKey;

  const userTokenAccount = new anchor.web3.PublicKey("HyHtUD3gsqEwGLLyMPb9D6BugbQagAme8SDsxTyQDXb");
  const userTokenMint = new anchor.web3.PublicKey("7fwA9aLTKmeueu9mJHU9QMBYEEfReMMMtkSoBBKXYwsH");

  const nftMint = new anchor.web3.PublicKey("2iqp3bj3rKGJ9KT1U5rSkD57cKD4sWrXEAQ79oTVNvbW");
  const nftTokenAccount = new anchor.web3.PublicKey("DhcxghpJWmmoRbwB5CBezS6mUkD1G8fiwhjzCTFhth92");
  const [nftMetadata] = anchor.web3.PublicKey.findProgramAddressSync([Buffer.from("metadata"), metadataProgram.toBuffer(), nftMint.toBuffer()], metadataProgram);
  const [allocation] = anchor.web3.PublicKey.findProgramAddressSync([nftMint.toBuffer(), userTokenMint.toBuffer()], program.programId);

  const [presaleTokenVault] = anchor.web3.PublicKey.findProgramAddressSync([Buffer.from("vault"), allocation.toBuffer()], program.programId);

  const presaleTokenUserAta = anchor.utils.token.associatedAddress({owner: user, mint: userTokenMint});

  const user2 = anchor.web3.Keypair.generate();

  it("Is initialized!", async () => {

    const token = await connection.getTokenAccountBalance(new anchor.web3.PublicKey("HyHtUD3gsqEwGLLyMPb9D6BugbQagAme8SDsxTyQDXb"));
    console.log(token);
    // Add your test here.
    try{
    const tx = await program.methods.createAllocation(100)
      .accounts({
        signer: user,
        sourceTokenAccount: userTokenAccount,
        allocation,
        presaleMint: userTokenMint,
        presaleTokenVault,
        allocationRefMint: nftMint,
        refMetadata: nftMetadata,
        tokenProgram: anchor.utils.token.TOKEN_PROGRAM_ID,
        systemProgram: anchor.web3.SystemProgram.programId
      }).rpc();
    console.log("Your transaction signature", tx);

    

    console.log(await program.account.allocation.fetch(allocation));

    const tx2 = await program.methods.claimAmount(10)
    .accounts({
      signer: user,
      allocation,
      allocationAuthority: user,
      presaleMint: userTokenMint,
      presaleTokenVault,
      userRecipientAccount: presaleTokenUserAta,
      userNftMetadata: nftMetadata,
      userNftTokenAccount: nftTokenAccount,
      tokenProgram: TOKEN_PROGRAM_ID,
      associatedTokenProgram: ASSOCIATED_PROGRAM_ID,
      systemProgram: anchor.web3.SystemProgram.programId
    }).rpc();

    let postBalance = await connection.getTokenAccountBalance(presaleTokenUserAta);
    console.log(postBalance);
    }
    catch(e){
      console.log(e);
    }
  });
});
