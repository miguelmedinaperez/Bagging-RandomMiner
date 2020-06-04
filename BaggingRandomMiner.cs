/*
 * Created by: Miguel Angel Medina Pérez (miguelmedinaperez@gmail.com)
 * Created: 11/16/2017
 * Comments by: Miguel Angel Medina Pérez (miguelmedinaperez@gmail.com)
 */

using PRFramework.Core.Common;
using PRFramework.Core.ComparisonFunctions;
using PRFramework.Core.Samplers;
using PRFramework.Core.SupervisedClassifiers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PRFramework.MigueExperimenter.AnomalyDetection
{
    public class BaggingRandomMiner
    {
        public int ClassifierCount { set; get; } = 100;

        public int BootstrapSamplePercent { set; get; } = 1;

        public bool UseBootstrapSampleCount { set; get; } = false;

        public int BootstrapSampleCount { set; get; } = 0;

        public bool UsePastEvenQueue { set; get; } = true;

        public double[] Classify(Instance instance)
        {
            var instanceVector = new InstanceVector(instance);
            double currentSimilarity = 0;
            for (int i = 0; i < ClassifierCount; i++)
            {
                double minDistance = double.MaxValue;
                foreach (var center in _centers[i])
                {
                    double d = _distance.Compare(instanceVector, center);
                    if (d < minDistance)
                        minDistance = d;
                }

                currentSimilarity += minDistance > 0 ? Math.Exp(-(minDistance * minDistance) / (2 * _sd[i] * _sd[i])) : 1;
            }
            currentSimilarity /= ClassifierCount;
            if (currentSimilarity < 0)
                currentSimilarity = 0;

            if (!UsePastEvenQueue)
                return new double[1] { 1-currentSimilarity };

            double resultSimilarity = (_alpha * _similaritySum / _maxEventCount + (1 - _alpha) * currentSimilarity);
            if (resultSimilarity < 0)
                resultSimilarity = 0;

            _similaritySum += currentSimilarity;
            if (_pastEvents.Count == _maxEventCount)
                _similaritySum -= _pastEvents.Dequeue();

            _pastEvents.Enqueue(currentSimilarity);

            if (_similaritySum < 0)
                _similaritySum = 0;

            return new double[1] { 1-resultSimilarity }; 
        }

        public void Train(InstanceModel model, IEnumerable<Instance> dataset)
        {
            var classFeature = model.ClassFeature() as NominalFeature;
            List<Feature> featuresToConsider = new List<Feature>();
            for (int i = 0; i < model.Features.Length; i++)
                if (model.Features[i] != classFeature)
                {
                    featuresToConsider.Add(model.Features[i]);
                }

            _distance = new EuclideanDissimilarity(dataset.ToList(), model);


            var instanceList = dataset.ToList();
            var randomSampler = new RandomSamplerWithReplacement<Instance>();
            _sd = new double[ClassifierCount];
            _centers = new IEnumerable<Instance>[ClassifierCount];

            for (int i = 0; i < ClassifierCount; i++)
            {
                int sampleSize = UseBootstrapSampleCount
                    ? BootstrapSampleCount : (BootstrapSamplePercent * instanceList.Count / 100);

                _centers[i] = randomSampler.GetSample(dataset, sampleSize);

                _sd[i] = ComputeBeta(_centers[i].ToList());
            }
        }

        private double ComputeBeta(List<Instance> prObjectVectorsList)
        {
            double sum = 0;
            int count = 0;
            for (int i = 0; i < prObjectVectorsList.Count - 1; i++)
                for (int j = i + 1; j < prObjectVectorsList.Count; j++)
                {
                    sum += _distance.Compare(prObjectVectorsList[i], prObjectVectorsList[j]);
                    count++;
                }

            return sum / count;
        }

        private EuclideanDissimilarity _distance;

        private double[] _sd = null;

        private IEnumerable<Instance>[] _centers = null;

        private Queue<double> _pastEvents = new Queue<double>();

        private double _similaritySum = 0;

        private const int _maxEventCount = 3;

        private const double _alpha = 0.5;
    }
}
