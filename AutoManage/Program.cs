using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Topshelf;

namespace AutoManage
{
    class Program
    {
        static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.ConfigureAndWatch(new FileInfo(AppDomain.CurrentDomain.BaseDirectory + "log4net.config"));
            HostFactory.Run(x =>
            {
                x.Service<ServiceRunner>();

                x.SetDescription("1收花时间2天后自动修改订单状态待评价2.订单半小时未支付取消订单3.收花时间7天后自动完成并评价");
                x.SetDisplayName("订单状态修改");
                x.SetServiceName("订单确认");

                x.EnablePauseAndContinue();
            });
        }
    }
}
