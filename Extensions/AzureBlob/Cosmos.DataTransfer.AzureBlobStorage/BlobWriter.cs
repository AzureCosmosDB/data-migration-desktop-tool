using Azure.Storage.Blobs.Specialized;

namespace Cosmos.DataTransfer.AzureBlobStorage
{
    public static class BlobWriter
    {
        private static BlockBlobClient blob;

        public static void InitializeAzureBlobClient(string connectionString, string containerName, string blobName)
        {
            blob = new BlockBlobClient(connectionString, containerName, blobName);
        }

        public static async Task WriteToAzureBlob(byte[] fileContents, int? maxBlockSize, CancellationToken cancellationToken)
        {
            int MAX_BLOCK_SIZE = 512000;
            if (maxBlockSize.HasValue && maxBlockSize.Value > 0)
            {
                MAX_BLOCK_SIZE = maxBlockSize.Value;
            }
            
            List<string> blockIds = new List<string>();
            int blockId = 0;
            int contentProcessed = 0;

            // Set current block size to MAX size
            int currentBlockSize = MAX_BLOCK_SIZE;

            while (currentBlockSize == MAX_BLOCK_SIZE)
            {
                // If content processed + current block size exceeds file length, 
                // then set current block size to difference of file length - content processed
                // this is done to capture the last block that is smaller than MAX block size
                if ((contentProcessed + currentBlockSize) > fileContents.Length)
                    currentBlockSize = fileContents.Length - contentProcessed;

                // Create an array consisting only the subset/block of the file content
                byte[] byteBlock = new byte[currentBlockSize];
                Array.Copy(fileContents, contentProcessed, byteBlock, 0, currentBlockSize);

                // Create a Base64 string for block ID. We can use any Base64 string, but be sure to hold the value to commit
                string blockID = Convert.ToBase64String(System.BitConverter.GetBytes(blockId));

                // We are staging/adding a new block to a blob in storage account, to be committed later
                blob.StageBlock(blockID, new MemoryStream(byteBlock, true), null, cancellationToken);

                // Adding block IDs to list
                blockIds.Add(blockID);

                // Increase total blocks created
                contentProcessed += currentBlockSize;
                blockId++;
            }

            // Commit all the blocks to storage account. Unless committed, we will not see the file in storage account
            blob.CommitBlockList(blockIds);

        }        
    }
}
