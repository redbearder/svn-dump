using System;
using System.Collections;
using System.Security;
using GodLesZ.Library.Amf.IO;
using GodLesZ.Library.Amf.Messaging.Messages;
using log4net;

namespace GodLesZ.Library.Amf.Messaging.Endpoints.Filter {
	/// <summary>
	/// This type supports the infrastructure and is not intended to be used directly from your code.
	/// </summary>
	class MessageFilter : AbstractFilter {
		private static readonly ILog log = LogManager.GetLogger(typeof(MessageFilter));
		EndpointBase _endpoint;

		public MessageFilter(EndpointBase endpoint) {
			_endpoint = endpoint;
		}

		#region IFilter Members

		public override void Invoke(AMFContext context) {
			MessageOutput messageOutput = context.MessageOutput;
			for (int i = 0; i < context.AMFMessage.BodyCount; i++) {
				AMFBody amfBody = context.AMFMessage.GetBodyAt(i);

				if (!amfBody.IsEmptyTarget)
					continue;

				object content = amfBody.Content;
				if (content is IList)
					content = (content as IList)[0];
				IMessage message = content as IMessage;

				//Check for Flex2 messages and handle
				if (message != null) {
					//if (FluorineContext.Current.Client == null)
					//    FluorineContext.Current.SetCurrentClient(_endpoint.GetMessageBroker().ClientRegistry.GetClient(message));

					if (message.clientId == null)
						message.clientId = Guid.NewGuid().ToString("D");

					//Check if response exists.
					ResponseBody responseBody = messageOutput.GetResponse(amfBody);
					if (responseBody != null) {
						continue;
					}

					try {
						if (context.AMFMessage.BodyCount > 1) {
							CommandMessage commandMessage = message as CommandMessage;
							//Suppress poll wait if there are more messages to process
							if (commandMessage != null && commandMessage.operation == CommandMessage.PollOperation)
								commandMessage.SetHeader(CommandMessage.FluorineSuppressPollWaitHeader, null);
						}
						IMessage resultMessage = _endpoint.ServiceMessage(message);
						if (resultMessage is ErrorMessage) {
							ErrorMessage errorMessage = resultMessage as ErrorMessage;
							responseBody = new ErrorResponseBody(amfBody, message, resultMessage as ErrorMessage);
							if (errorMessage.faultCode == ErrorMessage.ClientAuthenticationError) {
								messageOutput.AddBody(responseBody);
								for (int j = i + 1; j < context.AMFMessage.BodyCount; j++) {
									amfBody = context.AMFMessage.GetBodyAt(j);

									if (!amfBody.IsEmptyTarget)
										continue;

									content = amfBody.Content;
									if (content is IList)
										content = (content as IList)[0];
									message = content as IMessage;

									//Check for Flex2 messages and handle
									if (message != null) {
										responseBody = new ErrorResponseBody(amfBody, message, new SecurityException(errorMessage.faultString));
										messageOutput.AddBody(responseBody);
									}
								}
								//Leave further processing
								return;
							}
						} else {
							responseBody = new ResponseBody(amfBody, resultMessage);
						}
					} catch (Exception exception) {
						if (log != null && log.IsErrorEnabled)
							log.Error(exception.Message, exception);
						responseBody = new ErrorResponseBody(amfBody, message, exception);
					}
					messageOutput.AddBody(responseBody);
				}
			}
		}

		#endregion
	}
}
