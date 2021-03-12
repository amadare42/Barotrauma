﻿using System;
using System.Xml.Linq;

namespace Barotrauma.Items.Components
{
    class AndComponent : ItemComponent
    {        
        protected string output, falseOutput;

        //an array to keep track of how long ago a non-zero signal was received on both inputs
        protected float[] timeSinceReceived;

        //the output is sent if both inputs have received a signal within the timeframe
        protected float timeFrame;
        
        [InGameEditable(DecimalCount = 2), Serialize(0.0f, true, description: "The item sends the output if both inputs have received a non-zero signal within the timeframe. If set to 0, the inputs must receive a signal at the same time.", alwaysUseInstanceValues: true)]
        public float TimeFrame
        {
            get { return timeFrame; }
            set
            {
                timeFrame = Math.Max(0.0f, value);
            }
        }

        [InGameEditable, Serialize("1", true, description: "The signal sent when the condition is met.", alwaysUseInstanceValues: true)]
        public string Output
        {
            get { return output; }
            set
            {
                if (value == null) { return; }
                output = value;
                if (output.Length > MaxOutputLength)
                {
                    output = output.Substring(0, MaxOutputLength);
                }
            }
        }

        [InGameEditable, Serialize("", true, description: "The signal sent when the condition is met (if empty, no signal is sent).", alwaysUseInstanceValues: true)]
        public string FalseOutput
        {
            get { return falseOutput; }
            set
            {
                if (value == null) { return; }
                falseOutput = value;
                if (falseOutput.Length > MaxOutputLength)
                {
                    falseOutput = falseOutput.Substring(0, MaxOutputLength);
                }
            }
        }

        private int maxOutputLength;
        [Editable, Serialize(200, false, description: "The maximum length of the output strings. Warning: Large values can lead to large memory usage or networking issues.")]
        public int MaxOutputLength
        {
            get { return maxOutputLength; }
            set
            {
                maxOutputLength = Math.Max(value, 0);
            }
        }

        public AndComponent(Item item, XElement element)
            : base(item, element)
        {
            timeSinceReceived = new float[] { Math.Max(timeFrame * 2.0f, 0.1f), Math.Max(timeFrame * 2.0f, 0.1f) };
            IsActive = true;
        }

        public override void Update(float deltaTime, Camera cam)
        {
            bool sendOutput = true;
            for (int i = 0; i < timeSinceReceived.Length; i++)
            {
                if (timeSinceReceived[i] > timeFrame) sendOutput = false;
                timeSinceReceived[i] += deltaTime;
            }

            string signalOut = sendOutput ? output : falseOutput;
            if (string.IsNullOrEmpty(signalOut)) return;

            item.SendSignal(signalOut, "signal_out");
        }

        public override void ReceiveSignal(Signal signal, Connection connection)
        {
            switch (connection.Name)
            {
                case "signal_in1":
                    if (signal.value == "0") return;
                    timeSinceReceived[0] = 0.0f;
                    break;
                case "signal_in2":
                    if (signal.value == "0") return;
                    timeSinceReceived[1] = 0.0f;
                    break;
                case "set_output":
                    output = signal.value;
                    break;
            }
        }
    }
}
