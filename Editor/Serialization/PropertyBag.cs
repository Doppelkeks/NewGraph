using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NewGraph {
    /// <summary>
    /// This class is used to build and retrieve a collection of valid properties for the graph to display and operate on.
    /// This allows to autogenerate UIs only with special Attributes without the need for custom inspectors.
    /// This caches on a per type basis, as this logic relies on reflection.
    /// Think of it as a set of infos and instructions that can later be mindlessly & efficiently iterated when all the serialized data needs to be turned into UI 
    /// </summary>
    public class PropertyBag {

        public PortInfo inputPort = null;
        public List<PortInfo> ports = new List<PortInfo>();
        public List<PortInfo> portLists = new List<PortInfo>();
        public List<PropertyInfo> graphPropertiesAndGroups = new List<PropertyInfo>();
        private AttributesBag attributeBag = new AttributesBag();
        private Type nodeType;

        private static Dictionary<Type, PropertyBag> propertyBagLookup = new Dictionary<Type, PropertyBag>();
        private List<GroupInfo> groupInfoLookup = new List<GroupInfo>();
        private Dictionary<Type, Func<int>> attributebehaviors = null;

        #region "current" variables (for traversing)
        private string currentRelativePropertyPath;
        private object currentAttribute;
        private GraphDisplayAttribute currentGraphDisplayAttribute = null;
        private int currentSpacesCount = 0;
        private List<HeaderAttribute> currentHeaderAttributes = new List<HeaderAttribute>();
        private SerializedProperty currentProperty;
        #endregion

        private PropertyBag(NodeAttribute nodeAttribute, Type nodeType, SerializedProperty nodeProperty) {
            this.nodeType = nodeType;

            inputPort = new PortInfo(nodeProperty.propertyPath, nodeType, new PortAttribute(nodeAttribute.inputPortCapacity, PortDirection.Input), GraphSettings.Instance.defaultInputName);
            InitializeAttributebehaviors();
            RetrieveAll(nodeProperty);
        }

        /// <summary>
        /// initializes the attribute behaviors for every attribute type we want to react on.
        /// If you want to add or react to another attribute, extend the attributebehaviors and add a new local method.
        /// </summary>
        private void InitializeAttributebehaviors() {
            attributebehaviors = new Dictionary<Type, Func<int>>() {
                {typeof(HideInInspector), GetHideInInspectorAttribute },
                {typeof(PortAttribute), GetPortAttribute },
                {typeof(OutputAttribute), GetPortAttribute },
                {typeof(GraphDisplayAttribute), GetGraphDisplayAttribute },
                {typeof(HeaderAttribute), GetHeaderAttribute },
                {typeof(SpaceAttribute), GetSpaceAttribute },
                {typeof(PortListAttribute), GetPortListAttribute },
            };

            // base method for PortAttribute and PortListAttribute
            int GetPortAttributeBase(ref List<PortInfo> portInfosList, Type type, string portName=null) {
                foreach (object attribute in attributeBag.attributes) {
                    Type attributeType = attribute.GetType();
                    if (attributeType == typeof(SerializeReference)) {
                        portInfosList.Add(new PortInfo(currentRelativePropertyPath, type, (PortAttribute)currentAttribute, portName));
                        return currentProperty.depth;
                    }
                }
                Logger.LogAlways($"Invalid Port declaration detected! Be sure to add [SerializeReference] next to the [Output/PortList] attribute. Field: {attributeBag.type}. Related PropertyPath: {currentProperty.propertyPath}");
                return currentProperty.depth;
            }

            //[PortList]
            int GetPortListAttribute() {
                if (attributeBag.type == typeof(string) || (!typeof(IEnumerable<object>).IsAssignableFrom(attributeBag.type) && !attributeBag.type.IsArray)) {
                    Logger.LogAlways($"Wrong use of [PortList] attribute detected! Make sure to use it only on lists and arrays!");
                    return currentProperty.depth;
                }

                // transform the type into the actual field type, right now we have something like this: List<Node> as the type
                Type type;
                if (attributeBag.type.IsArray) {
                    type = attributeBag.type.GetElementType();
                } else {
                    type = attributeBag.type.GetGenericArguments()[0];
                }
                string listFieldName = currentProperty.displayName;
                return GetPortAttributeBase(ref portLists, type, listFieldName);
            }

            //[HideInInspector]
            int GetHideInInspectorAttribute() {
                return currentProperty.depth;
            }

            //[Port]
            int GetPortAttribute() {
                return GetPortAttributeBase(ref ports, attributeBag.type);
            }

            //[GraphDisplay]
            int GetGraphDisplayAttribute() {
                if (currentGraphDisplayAttribute == null) {
                    GraphDisplayAttribute attrib = (GraphDisplayAttribute)currentAttribute;
                    // if it was set to hide we dont want to show this property anywhere
                    if (attrib.displayType == DisplayType.Hide) {
                        return currentProperty.depth;
                    } else {
                        currentGraphDisplayAttribute = attrib;
                    }
                }
                return -1;
            }

            //[Header]
            int GetHeaderAttribute() {
                currentHeaderAttributes.Add((HeaderAttribute)currentAttribute);
                return -1;
            }

            //[Space]
            int GetSpaceAttribute() {
                currentSpacesCount++;
                return -1;
            }
        }


        public static PropertyBag GetCachedOrCreate(NodeAttribute nodeAttribute, Type nodeType, SerializedProperty nodeProperty) {
            if (propertyBagLookup.ContainsKey(nodeType)) {
                return propertyBagLookup[nodeType];
            } else {
                PropertyBag propertyBag = new PropertyBag(nodeAttribute, nodeType, nodeProperty);
                propertyBagLookup.Add(nodeType, propertyBag);
                return propertyBag;
            }
        }

        private void RetrieveAll(SerializedProperty nodeProperty) {
            SerializedProperty endProperty = nodeProperty.GetEndProperty();
            SerializedProperty property = nodeProperty.Copy();
            bool diveIntoChildren;
            int ignoreFurtherDepth = -1;

            // check if we have visible properties
            if (!property.NextVisible(true)) return;
            do {
                diveIntoChildren = true;

                // check if the current property is equal to the last properity
                if (SerializedProperty.EqualContents(property, endProperty)) {
                    break;
                }

                // skip diving into the children for elements where we don't want this
                if (property.propertyType == SerializedPropertyType.ManagedReference) {
                    diveIntoChildren = false;
                    //continue;
                }

                if (ignoreFurtherDepth >= 0) {
                    if (property.depth > ignoreFurtherDepth) {
                        continue;
                    }
                }

                if (IsRealArray(property) || IsTypeThatShouldNotEnter(property)) {
                    diveIntoChildren = false;
                }

                string relativePropertyPath = property.propertyPath.Replace(nodeProperty.propertyPath, "");
                relativePropertyPath = relativePropertyPath.Substring(1, relativePropertyPath.Length - 1);

                currentRelativePropertyPath = relativePropertyPath;
                currentProperty= property;

                ignoreFurtherDepth = RetrieveCurrentAttributes();

            } while (property.NextVisible(diveIntoChildren)); // go to the next property element
        }

        /// <summary>
        /// Retrieve attributes for the currently active property
        /// </summary>
        /// <returns></returns>
        private int RetrieveCurrentAttributes() {

            int ignoreFurtherDepth = -1;
            currentHeaderAttributes.Clear();
            currentGraphDisplayAttribute = null;
            currentSpacesCount = 0;

            // retrieve all attributes
            attributeBag.GetAttributes(nodeType, currentRelativePropertyPath, true);

            // remember: we are only on ONE property!
            // and every property can have SEVERAL attributes!
            foreach (object attribute in attributeBag.attributes) {
                Type attributeType = attribute.GetType();
                currentAttribute = attribute;

                // execute all attribute behaviors!
                if (attributebehaviors.ContainsKey(attributeType)) {
                    ignoreFurtherDepth = attributebehaviors[attributeType]();
                    if (ignoreFurtherDepth < 0) {
                        break;
                    }
                }
            }

            // avoid further iteration of this property as it has already been processed or should be ignored...
            if (ignoreFurtherDepth < 0) {
                GraphPropertyInfo graphProperty = new GraphPropertyInfo(currentRelativePropertyPath, currentHeaderAttributes, currentSpacesCount, currentGraphDisplayAttribute);
                // only add to list if this should not be hidden (this takes care of the edge case where the default display type was set to Hide)
                if (graphProperty.graphDisplay.displayType != DisplayType.Hide) {
                    // check if we have a generic object, that is not an array.
                    // if this is the case, we need to wrap it in a group and ignore the object itself, as all upcoming child properties will be drawn
                    if (!IsRealArray(currentProperty) && currentProperty.propertyType == SerializedPropertyType.Generic) {
                        // create a new group
                        GroupInfo groupInfo = new GroupInfo(currentProperty.displayName, currentRelativePropertyPath);
                        // check if the new group should be part of another group and add it to the group graphPropertiesAndGroups
                        FindGroupAndAdd(ref currentRelativePropertyPath, groupInfo);
                        // add new group to lookup
                        groupInfoLookup.Add(groupInfo);
                    } else {
                        // check if the property should be part of another group and add it to the group graphPropertiesAndGroups
                        FindGroupAndAdd(ref currentRelativePropertyPath, graphProperty);
                    }
                } else {
                    ignoreFurtherDepth = currentProperty.depth;
                }
            }

            //Debug.Log(ignoreFurtherDepth + " "+ relativePropertyPath);
            return ignoreFurtherDepth;
        }

        /// <summary>
        /// Tries to find the group info this property belongs to and adds it as a child
        /// Otherwise plainly adds it to the group data
        /// </summary>
        /// <param name="relativePath"></param>
        /// <param name="propertyInfo"></param>
        private void FindGroupAndAdd(ref string relativePath, PropertyInfo propertyInfo) {
            GroupInfo groupPropertyBelongsTo = null;
            if (groupInfoLookup.Count > 0) {
                // loop backwards to find the most inner group first!
                for (int i = groupInfoLookup.Count - 1; i >= 0; i--) {
                    GroupInfo groupInfo = groupInfoLookup[i];
                    if (relativePath.Contains(groupInfo.relativePropertyPath)) {
                        groupPropertyBelongsTo = groupInfo;
                        break;
                    }
                }
            }
            if (groupPropertyBelongsTo != null) {
                groupPropertyBelongsTo.graphProperties.Add(propertyInfo);
            } else {
                graphPropertiesAndGroups.Add(propertyInfo);
            }
        }

        /// <summary>
        /// Types where we don't want to enter the children
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        private static bool IsTypeThatShouldNotEnter(SerializedProperty property) {
            if (property.hasChildren) {
                return property.propertyType == SerializedPropertyType.ManagedReference ||
                       property.propertyType == SerializedPropertyType.Vector2 ||
                       property.propertyType == SerializedPropertyType.Vector2Int ||
                       property.propertyType == SerializedPropertyType.Vector3 ||
                       property.propertyType == SerializedPropertyType.Vector3Int ||
                       property.propertyType == SerializedPropertyType.Quaternion ||
                       property.propertyType == SerializedPropertyType.Vector4 ||
                       property.propertyType == SerializedPropertyType.Rect ||
                       property.propertyType == SerializedPropertyType.RectInt ||
                       property.propertyType == SerializedPropertyType.Bounds ||
                       property.propertyType == SerializedPropertyType.BoundsInt ||
                       property.propertyType == SerializedPropertyType.Hash128;
            }
            return false;
        }

        /// <summary>
        /// Check to retrieve if this is a "real" array as in not a string or char
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        private bool IsRealArray(SerializedProperty property) {
            return property.isArray && property.propertyType != SerializedPropertyType.Character && property.propertyType != SerializedPropertyType.String;
        }
    }
}
