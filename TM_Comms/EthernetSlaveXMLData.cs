namespace TM_Comms
{
    public class EthernetSlaveXMLData
    {
        // NOTE: Generated code may require at least .NET Framework 4.5 or .NET Core/Standard 2.0.
        /// <remarks/>
        [System.SerializableAttribute()]
        [System.ComponentModel.DesignerCategoryAttribute("code")]
        [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
        [System.Xml.Serialization.XmlRootAttribute(Namespace = "", IsNullable = false)]
        public partial class File
        {

            private byte versionField;

            private string formTypeField;

            private string commModeField;

            private FileSetting[] codeTableField;

            private string idField;

            /// <remarks/>
            public byte Version
            {
                get
                {
                    return this.versionField;
                }
                set
                {
                    this.versionField = value;
                }
            }

            /// <remarks/>
            public string FormType
            {
                get
                {
                    return this.formTypeField;
                }
                set
                {
                    this.formTypeField = value;
                }
            }

            /// <remarks/>
            public string CommMode
            {
                get
                {
                    return this.commModeField;
                }
                set
                {
                    this.commModeField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlArrayItemAttribute("Setting", IsNullable = false)]
            public FileSetting[] CodeTable
            {
                get
                {
                    return this.codeTableField;
                }
                set
                {
                    this.codeTableField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public string ID
            {
                get
                {
                    return this.idField;
                }
                set
                {
                    this.idField = value;
                }
            }
        }

        /// <remarks/>
        [System.SerializableAttribute()]
        [System.ComponentModel.DesignerCategoryAttribute("code")]
        [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
        public partial class FileSetting
        {

            private string itemField;

            private string descriptionField;

            private string dataTypeField;

            private byte dataLengthField;

            private string accessibilityField;

            private string note1Field;

            private string writableModeField;

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public string Item
            {
                get
                {
                    return this.itemField;
                }
                set
                {
                    this.itemField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public string Description
            {
                get
                {
                    return this.descriptionField;
                }
                set
                {
                    this.descriptionField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public string DataType
            {
                get
                {
                    return this.dataTypeField;
                }
                set
                {
                    this.dataTypeField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public byte DataLength
            {
                get
                {
                    return this.dataLengthField;
                }
                set
                {
                    this.dataLengthField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public string Accessibility
            {
                get
                {
                    return this.accessibilityField;
                }
                set
                {
                    this.accessibilityField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public string Note1
            {
                get
                {
                    return this.note1Field;
                }
                set
                {
                    this.note1Field = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public string WritableMode
            {
                get
                {
                    return this.writableModeField;
                }
                set
                {
                    this.writableModeField = value;
                }
            }
        }

    }
}
