using log4net;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoManage.QuartzJobs
{
    public sealed class TestJob : IJob
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(TestJob));
        public static int only = 1;
        public void Execute(IJobExecutionContext context)
        {
            if (only!=1)
            {
                _logger.InfoFormat("TestJob测试=上一次任务还没有运行完成");
                return;
            }
            only = 2;
            System.Threading.Thread.Sleep(15000);
            _logger.InfoFormat("TestJob测试");
            only = 1;
        }
    }
}
