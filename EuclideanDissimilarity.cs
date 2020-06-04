/*
 * Created by: Miguel Angel Medina Pérez (miguelmedinaperez@gmail.com)
 * Created: 05/18/2007
 * Comments by: Miguel Angel Medina Pérez (miguelmedinaperez@gmail.com)
 */

using System;
using System.Collections.Generic;
using System.Linq;
using PRFramework.Core.Common;
using PRFramework.Core.SupervisedClassifiers;

namespace PRFramework.Core.ComparisonFunctions
{
    /// <summary>
    ///     A generic implementation of Euclidean distance with normalization. 
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The constructor of the class requires a delegate <see cref="GetComponent{IVector}"/> to provide the values of the components of the vectors.
    ///     </para>
    ///     <para>
    ///         Las diferencias entre los valores de un componente se normalizan por la diferencia del valor máximo menos el valor mínimo de dicho componente.
    ///     </para>
    /// </remarks>
    /// <typeparam name="IVector">A type of vector.</typeparam>
    [Serializable]
    public class EuclideanDissimilarity
    {
        public EuclideanDissimilarity(IEnumerable<Instance> instances, InstanceModel model)
        {
            _instanceModel = model;
            var classFeature = model.ClassFeature();
            _features = _instanceModel.Features.Where(f => f != classFeature).ToList();

            int instanceCount = 0;

            foreach (var instance in instances)
            {
                if (_instanceModel != instance.Model)
                    throw new ArgumentOutOfRangeException(nameof(model), model, $"Unable to instantiate ${nameof(EuclideanDissimilarity)}: Object found with invalid ${nameof(InstanceModel)}.");

                if (_minFeatureValues != null)
                {
                    foreach (var feature in _features)
                        if (!Double.IsNaN(instance[feature]))
                        {
                            if (feature.FeatureType == FeatureType.Double || feature.FeatureType == FeatureType.Integer)
                            {
                                if (Double.IsNaN(_minFeatureValues[feature.Index]) || instance[feature] < _minFeatureValues[feature.Index])
                                    _minFeatureValues[feature.Index] = instance[feature];

                                if (Double.IsNaN(_maxFeatureValues[feature.Index]) || instance[feature] > _maxFeatureValues[feature.Index])
                                    _maxFeatureValues[feature.Index] = instance[feature];
                            }
                        }
                }
                else
                {
                    _minFeatureValues = new double[_features.Count];
                    _maxFeatureValues = new double[_features.Count];
                    foreach (var feature in _features)
                        if (Double.IsNaN(instance[feature]))
                        {
                            _minFeatureValues[feature.Index] = Double.NaN;
                            _maxFeatureValues[feature.Index] = Double.NaN;
                        }
                        else
                        {
                            if (feature.FeatureType == FeatureType.Double || feature.FeatureType == FeatureType.Integer)
                            {
                                _minFeatureValues[feature.Index] = instance[feature];
                                _maxFeatureValues[feature.Index] = instance[feature];
                            }
                            else if (feature.FeatureType == FeatureType.Nominal)
                            {
                                _minFeatureValues[feature.Index] = Double.NaN;
                                _maxFeatureValues[feature.Index] = Double.NaN;
                            }
                        }
                }

                instanceCount++;
            }

            if (instanceCount < 1)
                throw new ArgumentOutOfRangeException(nameof(instances), instances, $"Unable to instantiate ${nameof(EuclideanDissimilarity)}: empty vector collection.");

            _maxLessMin = new double[_minFeatureValues.Length];
            double validFeaturesCount = 0;
            for (int i = 0; i < _minFeatureValues.Length; i++)
            {
                _maxLessMin[i] = _maxFeatureValues[i] - _minFeatureValues[i];
                if (!Double.IsNaN(_maxLessMin[i]) || (_features[i].FeatureType == FeatureType.Nominal))
                    validFeaturesCount++;
            }

            _maxDissimilarity = Math.Sqrt(validFeaturesCount);
        }

        //public static List<NormalizedEuclideanDistance> CreateDistances(IList<Instance> instances,
        //    IEnumerable<IList<Feature>> collectionOfFeaturesCollections, InstanceModel model)
        //{
        //    if (collectionOfFeaturesCollections == null)
        //        throw new ArgumentNullException(nameof(collectionOfFeaturesCollections), $"Unable to instantiate ${nameof(NormalizedEuclideanDistance)}: null collection of features collections.");

        //    var distancesList = new List<NormalizedEuclideanDistance>();
        //    foreach (var featuresCollection in collectionOfFeaturesCollections)
        //        distancesList.Add(distancesList.Count == 0
        //            ? new NormalizedEuclideanDistance(instances, featuresCollection, model)
        //            : new NormalizedEuclideanDistance(distancesList[0]._maxLessMin, featuresCollection, model));

        //    return distancesList;
        //}

        public double Compare(Instance source, Instance compareTo)
        {
            try
            {
                double sum = 0;
                foreach (var feature in _features)
                    if (!Double.IsNaN(source[feature]) && !Double.IsNaN(compareTo[feature]))
                    {
                        if (feature.FeatureType == FeatureType.Double || feature.FeatureType == FeatureType.Integer)
                        {
                            if (!double.IsNaN(_maxLessMin[feature.Index]) && _maxLessMin[feature.Index] > 0)
                            {
                                double componentDiff = Math.Abs(source[feature] - compareTo[feature]) / _maxLessMin[feature.Index];
                                sum += componentDiff > 1 ? 1 : Math.Pow(componentDiff, 2);
                            }
                        }
                        else if (feature.FeatureType == FeatureType.Nominal &&
                                 (int)source[feature] != (int)compareTo[feature])
                            sum += 1;
                    }
                    else
                        sum += 1;

                return Math.Sqrt(sum) / _maxDissimilarity;
            }
            catch (Exception e)
            {
                if (_instanceModel.Features.Length != source.Model.Features.Length)
                    throw new ArgumentOutOfRangeException(nameof(source), source,
                        "Unable to compare objects: Invalid instance model");
                if (_instanceModel.Features.Length != compareTo.Model.Features.Length)
                    throw new ArgumentOutOfRangeException(nameof(compareTo), compareTo,
                        "Unable to compare objects: Invalid instance model");
                for (int i = 0; i < source.Model.Features.Length; i++)
                {
                    if (source.Model.Features[i].FeatureType != _instanceModel.Features[i].FeatureType)
                        throw new ArgumentOutOfRangeException(nameof(source), source,
                            "Unable to compare objects: Invalid instance model");
                    if (compareTo.Model.Features[i].FeatureType != _instanceModel.Features[i].FeatureType)
                        throw new ArgumentOutOfRangeException(nameof(compareTo), compareTo,
                            "Unable to compare objects: Invalid instance model");
                }

                throw;
            }
        }

        public double Compare(Instance source, Instance compareTo, IEnumerable<Feature> featureSubset)
        {
            try
            {
                double sum = 0;
                double validFeaturesCount = 0;
                foreach (var feature in featureSubset)
                {
                    if (!Double.IsNaN(source[feature]) && !Double.IsNaN(compareTo[feature]))
                    {
                        if (feature.FeatureType == FeatureType.Double || feature.FeatureType == FeatureType.Integer)
                        {
                            if (!double.IsNaN(_maxLessMin[feature.Index]) && _maxLessMin[feature.Index] > 0)
                            {
                                double componentDiff = Math.Abs(source[feature] - compareTo[feature]) / _maxLessMin[feature.Index];
                                sum += componentDiff > 1 ? 1 : Math.Pow(componentDiff, 2);
                            }
                        }
                        else if (feature.FeatureType == FeatureType.Nominal &&
                                 (int)source[feature] != (int)compareTo[feature])
                            sum += 1;
                    }
                    else
                        sum += 1;

                    validFeaturesCount++;
                }

                return Math.Sqrt(sum) / Math.Sqrt(validFeaturesCount);
            }
            catch (Exception e)
            {
                if (_instanceModel.Features.Length != source.Model.Features.Length)
                    throw new ArgumentOutOfRangeException(nameof(source), source,
                        "Unable to compare objects: Invalid instance model");
                if (_instanceModel.Features.Length != compareTo.Model.Features.Length)
                    throw new ArgumentOutOfRangeException(nameof(compareTo), compareTo,
                        "Unable to compare objects: Invalid instance model");
                for (int i = 0; i < source.Model.Features.Length; i++)
                {
                    if (source.Model.Features[i].FeatureType != _instanceModel.Features[i].FeatureType)
                        throw new ArgumentOutOfRangeException(nameof(source), source,
                            "Unable to compare objects: Invalid instance model");
                    if (compareTo.Model.Features[i].FeatureType != _instanceModel.Features[i].FeatureType)
                        throw new ArgumentOutOfRangeException(nameof(compareTo), compareTo,
                            "Unable to compare objects: Invalid instance model");
                }

                throw;
            }
        }

        public void UpdateTraining(Instance instance)
        {
            foreach (var feature in _features)
                if (!Double.IsNaN(instance[feature]))
                {
                    if (feature.FeatureType == FeatureType.Double || feature.FeatureType == FeatureType.Integer)
                    {
                        if (Double.IsNaN(_minFeatureValues[feature.Index]) || instance[feature] < _minFeatureValues[feature.Index])
                            _minFeatureValues[feature.Index] = instance[feature];

                        if (Double.IsNaN(_maxFeatureValues[feature.Index]) || instance[feature] > _maxFeatureValues[feature.Index])
                            _maxFeatureValues[feature.Index] = instance[feature];
                    }
                }

            _maxLessMin = new double[_minFeatureValues.Length];
            double validFeaturesCount = 0;
            for (int i = 0; i < _minFeatureValues.Length; i++)
            {
                _maxLessMin[i] = _maxFeatureValues[i] - _minFeatureValues[i];
                if (!Double.IsNaN(_maxLessMin[i]) || (_features[i].FeatureType == FeatureType.Nominal))
                    validFeaturesCount++;
            }

            _maxDissimilarity = Math.Sqrt(validFeaturesCount);
        }

        private double[] _minFeatureValues = null;
        private double[] _maxFeatureValues = null;
        private double[] _maxLessMin;
        private double _maxDissimilarity;
        private readonly List<Feature> _features;
        private readonly InstanceModel _instanceModel;
    }
}
