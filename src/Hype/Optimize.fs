﻿//
// This file is part of
// Hype: Machine Learning and Hyperparameter Optimization Library
//
// Copyright (c) 2015, National University of Ireland Maynooth (Atilim Gunes Baydin, Barak A. Pearlmutter)
//
// Hype is released under the MIT license.
// (See accompanying LICENSE file.)
//
// Written by:
//
//   Atilim Gunes Baydin
//   atilimgunes.baydin@nuim.ie
//
//   Barak A. Pearlmutter
//   barak@cs.nuim.ie
//
//   Brain and Computation Lab
//   Hamilton Institute & Department of Computer Science
//   National University of Ireland Maynooth
//   Maynooth, Co. Kildare
//   Ireland
//
//   www.bcl.hamilton.ie
//

namespace Hype

open FsAlg.Generic
open DiffSharp.AD
open DiffSharp.AD.Vector


type Rnd() =
    static let R = new System.Random()
    static member Next() = R.Next()
    static member Next(max) = R.Next(max)
    static member Next(min,max) = R.Next(min, max)
    static member NextD() = D (R.NextDouble())
    static member NextD(max) = 
        if max < D 0. then invalidArg "max" "Max should be nonnegative."
        R.NextDouble() * max
    static member NextD(min,max) = min + D (R.NextDouble()) * (max - min)

type LearningRate =
    | ConstantLearningRate of D
    | ScheduledLearningRate of Vector<D>

type Params =
    {Epochs : int
     MinibatchSize : int
     LearningRate : LearningRate
     LossFunction : Vector<D>->Vector<D>->D
     TrainFunction: Params->(Vector<D>*Vector<D>)[]->(Vector<D>->Vector<D>->Vector<D>)->Vector<D>->(Vector<D>*D)[]}
    static member Default =
        {Epochs = 100
         MinibatchSize = 3
         LearningRate = ConstantLearningRate (D 0.5)
         LossFunction = fun x y -> Vector.normSq (x - y)
         TrainFunction = Train.GD}



and Optimize =
    // w_t+1 = w_t - learningrate * grad Q(w)
    static member GD (par:Params) (q:Vector<D>->D) (w0:Vector<D>) =
        let w = ref (Vector.copy w0)
        match par.LearningRate with
        | ConstantLearningRate l ->
            [|for i in 0..par.Epochs do
                let v, g = grad' q !w
                yield !w, v
                w := !w - l * g|]
        | ScheduledLearningRate l ->
            [|for i in 0..l.Length - 1 do
                let v, g = grad' q !w
                yield !w, v
                w := !w - l.[i] * g|]

and Train =
    // y_i = f(w, x_i)
    // Q(w) = sum_i Loss(y_i, f(w, x_i))
    static member GD (par:Params) (t:(Vector<D>*Vector<D>)[]) (f:Vector<D>->Vector<D>->Vector<D>) (w0:Vector<D>)  =
        let q w = t |> Array.sumBy (fun (x, y) -> par.LossFunction y (f w x))
        Optimize.GD par q w0

    // y_i = f(w, x_i)
    // Q(w) = Loss(y_i, f(w, x_i)), i random
    /// Stochastic gradient descent
    static member SGD (par:Params) (t:(Vector<D>*Vector<D>)[]) (f:Vector<D>->Vector<D>->Vector<D>) (w0:Vector<D>) =
        let q w =
            Array.init par.MinibatchSize (fun _ -> t.[Rnd.Next(t.Length)]) // Create a minibatch of random pairs
            |> Array.sumBy (fun (x, y) -> par.LossFunction y (f w x)) // Sum the error of the minibatch
        Optimize.GD par q w0
