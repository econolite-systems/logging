// SPDX-License-Identifier: MIT
// Copyright: 2023 Econolite Systems, Inc.
using Econolite.Ode.Domain.VehiclePriority;
using Econolite.Ode.Extensions.AspNet;
using Econolite.Ode.Messaging.Extensions;
using Econolite.Ode.Monitoring.HealthChecks.Mongo.Extensions;
using Econolite.Ode.Persistence.Mongo;
using Econolite.Ode.Repository.Ess;
using Econolite.Ode.Repository.PavementCondition;
using Econolite.Ode.Repository.VehiclePriority;
using Econolite.Ode.Repository.WrongWayDriver;
using Econolite.Ode.Status.Ess.Messaging.Extensions;
using Econolite.Ode.Worker.Ess;
using Econolite.Ode.Worker.Logging;
using Econolite.Ode.Worker.PavementCondition;
using Econolite.Ode.Worker.WrongWayDriver;
using Consts = Econolite.Ode.Worker.Logging.Consts;

await AppBuilder.BuildAndRunWebHostAsync(args, options => { options.Source = "Logging Worker"; }, (builderContext, services) =>
{
    //add Kafka consumers
    services.AddMessaging();
    services.AddVehiclePriorityConsumers(options =>
    {
        options.MaxConcurrency = int.Parse(builderContext.Configuration[Consts.ScalingPriority] ?? throw new NullReferenceException($"{Consts.ScalingPriority} is missing from config."));
    });
    services.AddVehiclePriorityConsumers();
    services.AddEssStatusConsumer(options =>
    {
        options.ConfigTopic = Econolite.Ode.Messaging.Consts.TOPICS_DEVICESTATUS;
    });
    services.AddPavementConditionConsumers();
    services.AddWrongWayDriverConsumers("Topics:WrongWayDriverStatus");

    //add Mongo
    services.AddMongo();

    //add repositories
    services.AddEssStatusRepo();
    services.AddPavementConditionStatusRepository();
    services.AddVehicleRequestLogRespository();
    services.AddWrongWayDriverStatusRepository();

    //add logger services
    services.AddHostedService<EssStatusLogger>();
    services.AddHostedService<PavementConditionStatusLogger>();
    services.AddHostedService<VehiclePriorityLogger>();
    services.AddHostedService<WrongWayDriverLogger>();
}, (_, checksBuilder) => checksBuilder.AddMongoDbHealthCheck());
