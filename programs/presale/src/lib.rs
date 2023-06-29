use anchor_lang::prelude::*;
use anchor_lang::solana_program::native_token::LAMPORTS_PER_SOL;
use anchor_spl::associated_token::*;
use anchor_spl::metadata::*;
use anchor_spl::token;
use anchor_spl::token::*;

declare_id!("41rsNvgfAqEqdMsSxegNghscr1xVoHi4aGZNjrpccG84");

#[program]
pub mod presale {

    use super::*;

    pub fn create_allocation(ctx: Context<CreateAllocation>, allocation_amount: u16) -> Result<()> {
        let allocation = &mut ctx.accounts.allocation;
        let metadata = &mut ctx.accounts.ref_metadata;
        let allocation_is_creator = metadata.collection.is_none();
        allocation.creator_or_collection = match allocation_is_creator {
            true => {
                metadata
                    .data
                    .creators
                    .as_ref()
                    .unwrap()
                    .iter()
                    .find(|c| c.verified)
                    .unwrap()
                    .address
            }
            false => metadata.collection.as_ref().unwrap().key,
        };
        allocation.allocation_is_creator = allocation_is_creator;
        allocation.amount_allocated = allocation_amount;
        allocation.allocation_authority = ctx.accounts.signer.key();
        allocation.presale_token = ctx.accounts.presale_mint.key();
        allocation.ref_mint = ctx.accounts.allocation_ref_mint.key();

        let cpi_ctx = CpiContext::new(
            ctx.accounts.token_program.to_account_info(),
            token::Transfer {
                from: ctx.accounts.source_token_account.to_account_info(),
                to: ctx.accounts.presale_token_vault.to_account_info(),
                authority: ctx.accounts.signer.to_account_info(),
            },
        );
        token::transfer(cpi_ctx, allocation_amount.into())
    }

    pub fn close_allocation(ctx: Context<CloseAllocation>) -> Result<()> {
        let close_ctx = CpiContext::new(
            ctx.accounts.token_program.to_account_info(),
            token::Transfer {
                from: ctx.accounts.presale_token_vault.to_account_info(),
                to: ctx.accounts.signer.to_account_info(),
                authority: ctx.accounts.allocation.to_account_info(),
            },
        );

        token::transfer(
            close_ctx.with_signer(&[&[
                ctx.accounts.allocation.ref_mint.key().as_ref(),
                ctx.accounts.allocation.presale_token.as_ref(),
                &[*ctx.bumps.get("allocation").unwrap()],
            ]]),
            ctx.accounts.presale_token_vault.amount as u64,
        )
    }

    pub fn claim_amount(ctx: Context<ClaimAmount>, amount: u16) -> Result<()> {
        if amount as u64 > ctx.accounts.presale_token_vault.amount {
            panic!("Sold out")
        }
        ctx.accounts.allocation.amount_spent = ctx
            .accounts
            .allocation
            .amount_spent
            .checked_add(amount)
            .unwrap();
        if ctx.accounts.allocation.amount_spent > ctx.accounts.allocation.amount_allocated {
            panic!("Sold out")
        }
        let to_pay = 6 * LAMPORTS_PER_SOL * amount as u64;
        let payment_ctx = CpiContext::new(
            ctx.accounts.system_program.to_account_info(),
            anchor_lang::system_program::Transfer {
                from: ctx.accounts.signer.to_account_info(),
                to: ctx.accounts.allocation_authority.to_account_info(),
            },
        );
        anchor_lang::system_program::transfer(payment_ctx, to_pay)?;

        let sale_ctx = CpiContext::new(
            ctx.accounts.token_program.to_account_info(),
            token::Transfer {
                from: ctx.accounts.presale_token_vault.to_account_info(),
                to: ctx.accounts.user_recipient_account.to_account_info(),
                authority: ctx.accounts.allocation.to_account_info(),
            },
        );

        token::transfer(
            sale_ctx.with_signer(&[&[
                ctx.accounts.allocation.ref_mint.key().as_ref(),
                ctx.accounts.allocation.presale_token.as_ref(),
                &[*ctx.bumps.get("allocation").unwrap()],
            ]]),
            amount as u64,
        )
    }
}

#[derive(Accounts)]
pub struct Initialize {}

#[derive(Accounts)]
pub struct CreateAllocation<'info> {
    #[account(mut)]
    pub signer: Signer<'info>,
    #[account(mut, token::mint=presale_mint)]
    pub source_token_account: Account<'info, TokenAccount>,
    #[account(init, payer=signer, space=150, seeds=[allocation_ref_mint.key().as_ref(), presale_mint.key().as_ref()], bump)]
    pub allocation: Account<'info, Allocation>,
    #[account(constraint = presale_mint.key().to_string() == "ESyHCUfKeT1ffLNRfCsjyHzNL4qN22kruVr8vYkPDR5r"||presale_mint.key().to_string()=="C2PQaR8QnS3C7peWVgAY4L2guSL1TYuT3qq4vQ88DjDf" || presale_mint.key().to_string() == "7fwA9aLTKmeueu9mJHU9QMBYEEfReMMMtkSoBBKXYwsH")]
    pub presale_mint: Account<'info, Mint>,
    #[account(init, payer=signer, token::mint=presale_mint, token::authority=allocation, seeds=[b"vault", allocation.key().as_ref()], bump )]
    pub presale_token_vault: Account<'info, TokenAccount>,
    pub allocation_ref_mint: Account<'info, Mint>,
    #[account(constraint=ref_metadata.mint == allocation_ref_mint.key())]
    pub ref_metadata: Box<Account<'info, MetadataAccount>>,
    pub token_program: Program<'info, Token>,
    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
pub struct CloseAllocation<'info> {
    #[account(mut)]
    pub signer: Signer<'info>,
    #[account(init_if_needed, payer=signer, associated_token::mint = presale_mint, associated_token::authority=signer )]
    pub signer_token_account: Account<'info, TokenAccount>,
    #[account(mut, constraint=allocation.allocation_authority == signer.key(), close=signer, seeds=[allocation.ref_mint.key().as_ref(), presale_mint.key().as_ref()], bump)]
    pub allocation: Account<'info, Allocation>,
    #[account(constraint = presale_mint.key().to_string() == "ESyHCUfKeT1ffLNRfCsjyHzNL4qN22kruVr8vYkPDR5r"||presale_mint.key().to_string()=="C2PQaR8QnS3C7peWVgAY4L2guSL1TYuT3qq4vQ88DjDf" || presale_mint.key().to_string() == "7fwA9aLTKmeueu9mJHU9QMBYEEfReMMMtkSoBBKXYwsH")]
    pub presale_mint: Account<'info, Mint>,
    #[account(mut, close=signer, token::mint=presale_mint, token::authority=allocation, seeds=[b"vault", allocation.key().as_ref()], bump )]
    pub presale_token_vault: Account<'info, TokenAccount>,
    pub allocation_ref_mint: Account<'info, Mint>,
    pub token_program: Program<'info, Token>,
    pub associated_token_program: Program<'info, AssociatedToken>,
    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
pub struct ClaimAmount<'info> {
    #[account(mut)]
    pub signer: Signer<'info>,
    #[account(mut, has_one=allocation_authority, seeds=[allocation.ref_mint.key().as_ref(), presale_mint.key().as_ref()], bump)]
    pub allocation: Account<'info, Allocation>,
    /// CHECK: recipient
    #[account(mut)]
    pub allocation_authority: UncheckedAccount<'info>,
    #[account(address=allocation.presale_token)]
    pub presale_mint: Box<Account<'info, Mint>>,
    #[account(mut, seeds=[b"vault", allocation.key().as_ref()], bump, token::mint=presale_mint, token::authority=allocation )]
    pub presale_token_vault: Account<'info, TokenAccount>,
    #[account(init_if_needed, payer=signer, associated_token::mint = presale_mint, associated_token::authority=signer )]
    pub user_recipient_account: Account<'info, TokenAccount>,
    #[account(constraint = allocation.matches_metadata(&user_nft_metadata))]
    pub user_nft_metadata: Box<Account<'info, MetadataAccount>>,
    #[account(constraint=user_nft_token_account.mint == user_nft_metadata.mint && user_nft_token_account.owner == signer.key() && user_nft_token_account.amount == 1)]
    pub user_nft_token_account: Box<Account<'info, TokenAccount>>,
    pub token_program: Program<'info, Token>,
    pub associated_token_program: Program<'info, AssociatedToken>,
    pub system_program: Program<'info, System>,
}

#[account]
pub struct Allocation {
    pub allocation_authority: Pubkey,
    pub is_active: bool,
    pub allocation_is_creator: bool,
    pub creator_or_collection: Pubkey,
    pub ref_mint: Pubkey,
    pub presale_token: Pubkey,
    pub amount_allocated: u16,
    pub amount_spent: u16,
}

impl Allocation {
    pub fn matches_metadata(&self, metadata: &MetadataAccount) -> bool {
        self.creator_or_collection
            == match self.allocation_is_creator {
                true => {
                    metadata
                        .data
                        .creators
                        .as_ref()
                        .unwrap()
                        .iter()
                        .find(|c| c.verified)
                        .unwrap()
                        .address
                }
                false => {
                    if metadata.collection.as_ref().unwrap().verified {
                        metadata.collection.as_ref().unwrap().key
                    } else {
                        panic!("Not verified")
                    }
                }
            }
    }
}
