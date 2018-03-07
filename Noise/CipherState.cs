using System;

namespace Noise
{
	/// <summary>
	/// A CipherState can encrypt and decrypt data based on its variables k
	/// (a cipher key of 32 bytes) and n (an 8-byte unsigned integer nonce).
	/// </summary>
	internal sealed class CipherState<CipherType> : IDisposable where CipherType : Cipher, new()
	{
		private const ulong MaxNonce = UInt64.MaxValue;

		private static readonly byte[] zeroLen = new byte[0];
		private static readonly byte[] zeros = new byte[32];

		private readonly CipherType cipher = new CipherType();
		private byte[] k;
		private ulong n;
		private bool disposed;

		/// <summary>
		/// Sets k = key. Sets n = 0.
		/// </summary>
		public void InitializeKey(byte[] key)
		{
			if (k != null)
			{
				Array.Clear(k, 0, k.Length);
			}

			k = key;
			n = 0;
		}

		/// <summary>
		/// Returns true if k is non-empty, false otherwise.
		/// </summary>
		public bool HasKey()
		{
			return k != null;
		}

		/// <summary>
		///  Sets n = nonce. This function is used for handling out-of-order transport messages.
		/// </summary>
		public void SetNonce(ulong nonce)
		{
			n = nonce;
		}

		/// <summary>
		/// If k is non-empty returns ENCRYPT(k, n++, ad, plaintext).
		/// Otherwise returns plaintext.
		/// </summary>
		public int EncryptWithAd(byte[] ad, ReadOnlySpan<byte> plaintext, Span<byte> ciphertext)
		{
			if (n == MaxNonce)
			{
				throw new OverflowException("Nonce has reached its maximum value.");
			}

			if (ciphertext.Length < plaintext.Length + Constants.TagSize)
			{
				throw new ArgumentException("Buffer too small to hold the ciphertext.", nameof(ciphertext));
			}

			if (k == null)
			{
				plaintext.CopyTo(ciphertext);
				return plaintext.Length;
			}

			int bytesWritten = cipher.Encrypt(k, n, ad, plaintext, ciphertext);
			++n;

			return bytesWritten;
		}

		/// <summary>
		/// If k is non-empty returns DECRYPT(k, n++, ad, ciphertext).
		/// Otherwise returns ciphertext. If an authentication failure
		/// occurs in DECRYPT() then n is not incremented and an error
		/// is signaled to the caller.
		/// </summary>
		public int DecryptWithAd(byte[] ad, ReadOnlySpan<byte> ciphertext, Span<byte> plaintext)
		{
			if (n == MaxNonce)
			{
				throw new OverflowException("Nonce has reached its maximum value.");
			}

			if (plaintext.Length < ciphertext.Length - Constants.TagSize)
			{
				throw new ArgumentException("Buffer too small to hold the plaintext.", nameof(plaintext));
			}

			if (k == null)
			{
				ciphertext.CopyTo(plaintext);
				return ciphertext.Length;
			}

			int bytesRead = cipher.Decrypt(k, n, ad, ciphertext, plaintext);
			++n;

			return bytesRead;
		}

		/// <summary>
		/// Sets k = REKEY(k).
		/// </summary>
		public void Rekey()
		{
			k = k ?? new byte[Constants.KeySize];
			cipher.Encrypt(k, MaxNonce, zeroLen, zeros, k);
		}

		/// <summary>
		/// Disposes the object.
		/// </summary>
		public void Dispose()
		{
			if (!disposed)
			{
				InitializeKey(null);
				disposed = true;
			}
		}
	}
}
