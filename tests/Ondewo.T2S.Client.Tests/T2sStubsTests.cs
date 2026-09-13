using System;
using System.Linq;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Ondewo.T2S;
using Xunit;

namespace Ondewo.T2S.Client.Tests
{
    /// <summary>
    /// The product-specific half of the suite: concrete assertions against the ONDEWO T2S API,
    /// spelled out with real message, field, enum and RPC names.
    /// <para>
    /// This is the only test file that has to be rewritten when the setup is replicated to another
    /// ONDEWO product - <see cref="GeneratedStubsTests"/> carries over unchanged.
    /// </para>
    /// </summary>
    public class T2sStubsTests
    {
        private const string DummyTarget = "http://localhost:50051";

        [Fact]
        public void SynthesizeResponseRoundTripsEveryScalarFieldKind()
        {
            var response = new SynthesizeResponse
            {
                AudioUuid = "0f8a-4c2e",
                Audio = ByteString.CopyFromUtf8("RIFF....WAVE"),
                GenerationTime = 0.25f,
                AudioLength = 1.5f,
                Text = "Guten Morgen",
                NormalizedText = "guten morgen",
                SampleRate = 22050f,
                Config = new RequestConfig { T2SPipelineId = "pipeline-de-1" },
            };

            byte[] bytes = response.ToByteArray();
            SynthesizeResponse parsed = SynthesizeResponse.Parser.ParseFrom(bytes);

            Assert.NotEmpty(bytes);
            Assert.Equal(response, parsed);
            Assert.Equal("0f8a-4c2e", parsed.AudioUuid);
            Assert.Equal(ByteString.CopyFromUtf8("RIFF....WAVE"), parsed.Audio);
            Assert.Equal(0.25f, parsed.GenerationTime);
            Assert.Equal(1.5f, parsed.AudioLength);
            Assert.Equal("Guten Morgen", parsed.Text);
            Assert.Equal("guten morgen", parsed.NormalizedText);
            Assert.Equal(22050f, parsed.SampleRate);
            Assert.Equal("pipeline-de-1", parsed.Config.T2SPipelineId);
        }

        [Fact]
        public void SynthesizeRequestRoundTripsItsNestedConfig()
        {
            var request = new SynthesizeRequest
            {
                Text = "Guten Morgen",
                Config = new RequestConfig
                {
                    T2SPipelineId = "pipeline-de-1",
                    Pcm = Pcm._16,
                    AudioFormat = AudioFormat.Wav,
                },
            };

            SynthesizeRequest parsed = SynthesizeRequest.Parser.ParseFrom(request.ToByteArray());

            Assert.Equal(request, parsed);
            Assert.Equal("Guten Morgen", parsed.Text);
            Assert.Equal("pipeline-de-1", parsed.Config.T2SPipelineId);
            Assert.Equal(Pcm._16, parsed.Config.Pcm);
            Assert.Equal(AudioFormat.Wav, parsed.Config.AudioFormat);
        }

        [Fact]
        public void RepeatedFieldRoundTripsThroughAListResponse()
        {
            var response = new ListT2sPipelinesResponse();
            response.Pipelines.Add(new Text2SpeechConfig
            {
                Id = "pipeline-1",
                Active = true,
                Description = new T2SDescription { Language = "de", SpeakerName = "ondewo" },
            });
            response.Pipelines.Add(new Text2SpeechConfig { Id = "pipeline-2" });

            ListT2sPipelinesResponse parsed =
                ListT2sPipelinesResponse.Parser.ParseFrom(response.ToByteArray());

            Assert.Equal(response, parsed);
            Assert.Equal(
                new[] { "pipeline-1", "pipeline-2" },
                parsed.Pipelines.Select(pipeline => pipeline.Id));
            Assert.True(parsed.Pipelines[0].Active);
            Assert.Equal("de", parsed.Pipelines[0].Description.Language);
            Assert.Equal("ondewo", parsed.Pipelines[0].Description.SpeakerName);
        }

        [Fact]
        public void NestedRepeatedMessagesRoundTripThroughACustomPhonemizer()
        {
            var phonemizer = new CustomPhonemizerProto { Id = "phonemizer-1" };
            phonemizer.Maps.Add(new Map { Word = "ondewo", PhonemeGroups = "ˈɔndeːvo" });
            phonemizer.Maps.Add(new Map { Word = "tomato", PhonemeGroups = "təˈmeɪtoʊ" });

            CustomPhonemizerProto parsed =
                CustomPhonemizerProto.Parser.ParseFrom(phonemizer.ToByteArray());

            Assert.Equal(phonemizer, parsed);
            Assert.Equal("phonemizer-1", parsed.Id);
            Assert.Equal(new[] { "ondewo", "tomato" }, parsed.Maps.Select(map => map.Word));
            Assert.Equal("ˈɔndeːvo", parsed.Maps[0].PhonemeGroups);
        }

        [Fact]
        public void UnsetScalarFieldsCarryTheProto3DefaultsAndStayOffTheWire()
        {
            var response = new SynthesizeResponse();

            Assert.Equal(string.Empty, response.AudioUuid);
            Assert.Equal(ByteString.Empty, response.Audio);
            Assert.Equal(0f, response.GenerationTime);
            Assert.Null(response.Config);
            Assert.Empty(response.ToByteArray());
        }

        /// <summary>
        /// <c>RequestConfig.instruction</c> is a proto3 <c>optional</c> field, so it has explicit
        /// presence: the empty string is a value the client can actually send, and it is
        /// distinguishable from "not set" on the wire.
        /// </summary>
        [Fact]
        public void Proto3OptionalFieldKeepsExplicitPresence()
        {
            var unset = new RequestConfig { T2SPipelineId = "pipeline-de-1" };

            Assert.False(unset.HasInstruction);
            Assert.Equal(string.Empty, unset.Instruction);

            var explicitlyEmpty = new RequestConfig
            {
                T2SPipelineId = "pipeline-de-1",
                Instruction = string.Empty,
            };

            Assert.True(explicitlyEmpty.HasInstruction);

            // The generated Equals compares VALUES, not presence, so the two compare equal. The
            // distinction lives on the wire and in HasInstruction - which is the whole point of an
            // explicit-presence field: the empty string is a value the client can actually send.
            Assert.Equal(unset, explicitlyEmpty);
            Assert.True(
                explicitlyEmpty.ToByteArray().Length > unset.ToByteArray().Length,
                "an explicitly-set empty optional field has to reach the wire");

            RequestConfig parsed = RequestConfig.Parser.ParseFrom(explicitlyEmpty.ToByteArray());

            Assert.True(parsed.HasInstruction);
            Assert.Equal(string.Empty, parsed.Instruction);

            parsed.ClearInstruction();
            Assert.False(parsed.HasInstruction);
            Assert.Equal(unset.ToByteArray(), parsed.ToByteArray());
        }

        /// <summary>
        /// Most tuning knobs of <c>RequestConfig</c> sit in single-member oneofs, which is this
        /// API's way of giving a scalar explicit presence - selecting one has to flip its case.
        /// </summary>
        [Fact]
        public void SingleMemberOneofsTrackTheirCase()
        {
            var config = new RequestConfig { T2SPipelineId = "pipeline-de-1" };

            Assert.False(config.HasLengthScale);
            Assert.Equal(RequestConfig.OneofLengthScaleOneofCase.None, config.OneofLengthScaleCase);

            config.LengthScale = 1.25f;

            Assert.True(config.HasLengthScale);
            Assert.Equal(
                RequestConfig.OneofLengthScaleOneofCase.LengthScale,
                config.OneofLengthScaleCase);

            RequestConfig parsed = RequestConfig.Parser.ParseFrom(config.ToByteArray());

            Assert.Equal(1.25f, parsed.LengthScale);
            Assert.True(parsed.HasLengthScale);

            parsed.ClearLengthScale();

            Assert.False(parsed.HasLengthScale);
            Assert.Equal(RequestConfig.OneofLengthScaleOneofCase.None, parsed.OneofLengthScaleCase);
        }

        [Fact]
        public void EnumsStartAtTheirZeroValue()
        {
            Assert.Equal(0, (int)Pcm._16);
            Assert.Equal(Pcm._16, default(Pcm));
            Assert.Equal(0, (int)AudioFormat.Wav);
            Assert.Equal(AudioFormat.Wav, default(AudioFormat));

            // The C# name is PascalCased and loses the enum-name prefix; the wire/JSON name is the
            // one the server speaks.
            Assert.Equal(
                "PCM_16",
                Pcm._16.GetType()
                    .GetField(nameof(Pcm._16))
                    .GetCustomAttributes(typeof(Google.Protobuf.Reflection.OriginalNameAttribute), false)
                    .Cast<Google.Protobuf.Reflection.OriginalNameAttribute>()
                    .Single()
                    .Name);
        }

        [Fact]
        public void EnumFieldRoundTripsANonDefaultValue()
        {
            var request = new UpdateCustomPhonemizerRequest
            {
                Id = "phonemizer-1",
                UpdateMethod = UpdateCustomPhonemizerRequest.Types.UpdateMethod.Replace,
            };

            UpdateCustomPhonemizerRequest parsed =
                UpdateCustomPhonemizerRequest.Parser.ParseFrom(request.ToByteArray());

            Assert.Equal(
                UpdateCustomPhonemizerRequest.Types.UpdateMethod.Replace,
                parsed.UpdateMethod);
            Assert.NotEmpty(request.ToByteArray());
        }

        [Fact]
        public void Text2SpeechClientBindsToAChannelAndExposesTheDeclaredRpcs()
        {
            using GrpcChannel channel = GrpcChannel.ForAddress(DummyTarget);

            var client = new Text2Speech.Text2SpeechClient(channel);

            Assert.NotNull(client);
            Assert.Equal("ondewo.t2s.Text2Speech", Text2Speech.Descriptor.FullName);
            Assert.Contains(Text2Speech.Descriptor.Methods, method => method.Name == "Synthesize");

            string[] clientMethods = typeof(Text2Speech.Text2SpeechClient)
                .GetMethods()
                .Select(method => method.Name)
                .Distinct()
                .ToArray();

            foreach (string rpc in new[]
                     {
                         "Synthesize", "BatchSynthesize", "NormalizeText", "GetT2sPipeline",
                         "CreateT2sPipeline", "DeleteT2sPipeline", "UpdateT2sPipeline",
                         "ListT2sPipelines", "ListT2sLanguages", "ListT2sDomains",
                         "ListT2sNormalizationPipelines", "GetServiceInfo", "GetCustomPhonemizer",
                         "CreateCustomPhonemizer", "DeleteCustomPhonemizer", "UpdateCustomPhonemizer",
                         "ListCustomPhonemizer", "VoiceCloning",
                     })
            {
                Assert.Contains(rpc, clientMethods);
                Assert.Contains(rpc + "Async", clientMethods);
            }
        }

        /// <summary>
        /// <c>StreamingSynthesize</c> is the one bidirectional-streaming RPC of this API. A
        /// streaming RPC is generated without the unary/Async pair - every overload returns the
        /// duplex call object directly - so getting the stream kind wrong is a compile-time break
        /// for consumers, not a runtime one.
        /// </summary>
        [Fact]
        public void StreamingSynthesizeIsGeneratedAsABidirectionalStreamingCall()
        {
            Google.Protobuf.Reflection.MethodDescriptor rpc = Assert.Single(
                Text2Speech.Descriptor.Methods, method => method.IsClientStreaming);

            Assert.Equal("StreamingSynthesize", rpc.Name);
            Assert.True(rpc.IsServerStreaming);
            Assert.Equal(StreamingSynthesizeRequest.Descriptor, rpc.InputType);
            Assert.Equal(StreamingSynthesizeResponse.Descriptor, rpc.OutputType);

            System.Reflection.MethodInfo[] overloads = typeof(Text2Speech.Text2SpeechClient)
                .GetMethods()
                .Where(method => method.Name == "StreamingSynthesize")
                .ToArray();

            Assert.NotEmpty(overloads);
            Assert.All(
                overloads,
                method => Assert.Equal(
                    typeof(AsyncDuplexStreamingCall<StreamingSynthesizeRequest, StreamingSynthesizeResponse>),
                    method.ReturnType));
            Assert.DoesNotContain(
                "StreamingSynthesizeAsync",
                typeof(Text2Speech.Text2SpeechClient).GetMethods().Select(method => method.Name));
        }
    }
}
