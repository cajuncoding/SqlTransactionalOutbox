using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutbox.Receiving
{
    public enum OutboxReceivedItemProcessingStatus
    {
        /// <summary>
        /// Will acknowledge that the item has been fully received and handled successfully; this will mark
        /// the message as completed on the Publishing mechanism (e.g. Azure Service Bus) so that it won't be re-queued
        /// to be received again.
        /// </summary>
        AcknowledgeSuccessfulReceipt,
        
        /// <summary>
        /// Rejects the item as it could not be handled as expected and/or needs to be abandoned so that
        /// the publishing mechanism will re-queue it to be processed again.
        /// </summary>
        RejectAndAbandon,

        /// <summary>
        /// Rejects the item as something that cannot be handled at all, and therefore must be sent directly to the DeadLetter
        /// Queue.  For FIFO processing, this is the only valid Rejection status as any time that is abandoned would either
        /// a) create an infinite loop of processing because nothing after it could be received, or b) would allow processing
        /// of items out of order.
        /// </summary>
        RejectAsDeadLetter
    }
}
